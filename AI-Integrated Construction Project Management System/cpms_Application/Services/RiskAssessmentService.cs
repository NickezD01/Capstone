using cpms_Application.Interfaces;
using cpms_Application.Response;
using cpms_Application.Response.RiskAssessment;
using cpms_Domain;
using cpms_Domain.Models;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text;
using System.Text.Json;
using DomainTaskStatus = cpms_Domain.Models.TaskStatus;

namespace cpms_Application.Services
{
    public class RiskAssessmentService : IRiskAssessmentService
    {
        private const decimal ProgressGapTolerancePct = 10;
        private const decimal ProgressDeviationThresholdPct = 25;
        private const int DeadlineNearDays = 14;
        private const int AvailabilityWindowDays = 7;
        private const decimal BudgetWarnRatio = 0.8m;
        private const decimal BudgetCriticalRatio = 1.0m;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private static readonly HashSet<string> KnownRiskTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            ProjectRiskTypes.ScheduleDelay,
            ProjectRiskTypes.WorkItemDelay,
            ProjectRiskTypes.OverallProjectDelay,
            ProjectRiskTypes.MaterialShortage,
            ProjectRiskTypes.MaterialAvailability,
            ProjectRiskTypes.ProgressDeviation,
            ProjectRiskTypes.BudgetRisk
        };

        private static readonly HashSet<string> ActionOwnerRoles = new(StringComparer.OrdinalIgnoreCase)
        {
            Role.PM.ToString(),
            Role.WAREHOUSE_MANAGER.ToString()
        };

        private readonly IUnitOfWork _uow;
        private readonly IClaimService _claimService;
        private readonly IProjectAccessService _projectAccess;
        private readonly IWarehouseContext _warehouseContext;
        private readonly IGoogleAIClient? _googleAIClient;

        public RiskAssessmentService(
            IUnitOfWork uow,
            IClaimService claimService,
            IProjectAccessService? projectAccess = null,
            IWarehouseContext? warehouseContext = null,
            IGoogleAIClient? googleAIClient = null)
        {
            _uow = uow;
            _claimService = claimService;
            _projectAccess = projectAccess ?? new ProjectAccessService(uow, claimService);
            _warehouseContext = warehouseContext ?? new WarehouseContext(uow);
            _googleAIClient = googleAIClient;
        }

        public async Task<ApiResponse> GetProjectRisksAsync(int projectId)
        {
            var user = _claimService.GetUserClaim();
            var project = await _uow.Projects.GetByIdAsync(projectId);
            if (project == null)
                return new ApiResponse().SetNotFound("Project not found.");
            var isAdmin = string.Equals(user.Role, Role.ADMIN.ToString(), StringComparison.OrdinalIgnoreCase);
            if (!isAdmin && !_projectAccess.IsOwningProjectManager(project))
                return new ApiResponse().SetApiResponse(HttpStatusCode.Forbidden, false,
                    "Only the owning project manager may view risk assessments.");

            var (tasks, risks, _) = await AssessAsync(project);
            return new ApiResponse().SetOk(new ProjectRiskResponse
            {
                ProjectId = projectId,
                GeneratedAt = DateTime.UtcNow,
                Risks = risks
            });
        }

        public async Task<ApiResponse> RecommendActionsAsync(int projectId)
        {
            var project = await _uow.Projects.GetByIdAsync(projectId);
            if (project == null)
                return new ApiResponse().SetNotFound("Project not found.");
            if (!_projectAccess.IsOwningProjectManager(project))
                return new ApiResponse().SetApiResponse(HttpStatusCode.Forbidden, false,
                    "Only the owning project manager may request risk recommendations.");

            var (tasks, risks, spend) = await AssessAsync(project);
            if (risks.Count == 0)
            {
                return new ApiResponse().SetOk(new RecommendRiskActionsResponse
                {
                    ProjectId = projectId,
                    GeneratedAt = DateTime.UtcNow,
                    Actions = new List<RiskActionItemResponse>()
                });
            }
            if (_googleAIClient == null)
            {
                return new ApiResponse().SetApiResponse(HttpStatusCode.ServiceUnavailable, false,
                    "AI recommendations are not configured.");
            }

            var prompt = BuildRecommendationPrompt(project, tasks, risks, spend);
            var aiResult = await _googleAIClient.GenerateTextAsync(BuildRecommendationInstruction(), prompt);
            if (!aiResult.IsSuccess)
            {
                if (aiResult.IsRateLimited)
                {
                    return new ApiResponse().SetApiResponse(
                        HttpStatusCode.TooManyRequests,
                        false,
                        "Gemini rate limit exceeded. Wait a minute and try again.",
                        new { errorCode = "GEMINI_RATE_LIMITED" });
                }

                return new ApiResponse().SetBadRequest(aiResult.ErrorMessage ?? "AI request failed.");
            }

            AiRiskActionPlan? plan = null;
            var json = ExtractJsonObject(aiResult.Text);
            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    plan = JsonSerializer.Deserialize<AiRiskActionPlan>(json, JsonOptions);
                }
                catch (JsonException ex)
                {
                    return new ApiResponse().SetBadRequest(
                        new { errorCode = "AI_JSON_INVALID", detail = ex.Message },
                        "AI returned invalid recommendation JSON. Please try again.");
                }
            }
            if (plan == null)
            {
                return new ApiResponse().SetBadRequest(
                    new { errorCode = "AI_JSON_INVALID", detail = "No JSON object was found in the AI response." },
                    "AI returned invalid recommendation JSON. Please try again.");
            }

            var contractError = ValidateActionContract(plan);
            if (contractError != null)
            {
                return new ApiResponse().SetBadRequest(
                    new { errorCode = "AI_JSON_CONTRACT_INVALID", detail = contractError },
                    "AI returned recommendations that do not match the expected contract.");
            }

            return new ApiResponse().SetOk(new RecommendRiskActionsResponse
            {
                ProjectId = projectId,
                GeneratedAt = DateTime.UtcNow,
                Actions = plan.Actions
                    .OrderBy(a => a.Priority)
                    .Select(a => new RiskActionItemResponse
                    {
                        Priority = a.Priority,
                        Title = a.Title!.Trim(),
                        Detail = a.Detail?.Trim() ?? string.Empty,
                        OwnerRole = a.OwnerRole!.Trim().ToUpperInvariant(),
                        RelatedRiskTypes = a.RelatedRiskTypes.Select(t => t.Trim().ToUpperInvariant()).ToList()
                    })
                    .ToList()
            });
        }

        private async Task<(List<TaskItem> Tasks, List<RiskItemResponse> Risks, decimal Spend)> AssessAsync(Project project)
        {
            var today = DateTime.UtcNow.Date;
            var tasks = (await _uow.TaskItems.GetAllAsync(t => t.ProjectId == project.ProjectId))
                .OrderBy(t => t.TaskId).ToList();
            var openTasks = tasks
                .Where(t => t.Status is not (DomainTaskStatus.COMPLETED or DomainTaskStatus.CANCELLED or DomainTaskStatus.REJECTED))
                .ToList();
            var risks = new List<RiskItemResponse>();

            risks.AddRange(DetectScheduleRisks(openTasks, today));
            risks.AddRange(await DetectMaterialRisksAsync(project.ProjectId, openTasks, today));

            var spend = await MaterialBudgetLedger.GetProjectSpendAsync(_uow, project.ProjectId);
            risks.AddRange(DetectBudgetRisks(project, tasks, spend));
            risks.AddRange(DetectOverallDelay(project, tasks, risks, today));

            return (tasks, risks, spend);
        }

        private static string BuildRecommendationInstruction() =>
            "You are a construction project advisor. Propose concrete corrective actions for the risks listed. " +
            "Return only valid JSON matching the required schema. Do not include Markdown, comments, explanations, or code fences. " +
            "Every action must reference at least one of the listed risk types. Keep titles short and details actionable.";

        private static string BuildRecommendationPrompt(
            Project project, List<TaskItem> tasks, List<RiskItemResponse> risks, decimal spend)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"Project: {project.ProjectName} (baseline {project.BaselineStart:yyyy-MM-dd} to {project.BaselineEnd:yyyy-MM-dd}, " +
                $"budget {project.TotalProjectBudget} {project.Currency}, spent {spend}).");
            builder.AppendLine("Open tasks (name | baseline | progress | budget):");
            foreach (var task in tasks
                .Where(t => t.Status is not (DomainTaskStatus.COMPLETED or DomainTaskStatus.CANCELLED or DomainTaskStatus.REJECTED))
                .Take(50))
            {
                builder.AppendLine($"- {task.TaskName} ({task.BaselineStart:yyyy-MM-dd} to {task.BaselineEnd:yyyy-MM-dd}, " +
                    $"{task.ActualProgressPct}% complete, budget {task.PlannedBudget})");
            }
            builder.AppendLine("Active risks (type | severity | message):");
            foreach (var risk in risks.Take(50))
            {
                var scope = risk.TaskId.HasValue ? $"task {risk.TaskName} " : string.Empty;
                builder.AppendLine($"- [{risk.RiskType}] [{risk.Severity}] {scope}{risk.Message}");
            }
            builder.AppendLine("Owner roles are PM (planning, approvals, rescheduling) and WAREHOUSE_MANAGER (stock, procurement, issuance).");
            builder.AppendLine("Return JSON with this exact top-level shape:");
            builder.AppendLine("""
{
  "actions": [
    { "priority": 1, "title": "string", "detail": "string", "ownerRole": "PM or WAREHOUSE_MANAGER", "relatedRiskTypes": ["RISK_TYPE"] }
  ]
}
""");
            builder.AppendLine("At most 10 actions, priority 1 highest to 5 lowest.");
            return builder.ToString();
        }

        private static string? ExtractJsonObject(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;
            var trimmed = text.Trim();
            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                var firstLineEnd = trimmed.IndexOf('\n');
                if (firstLineEnd >= 0)
                    trimmed = trimmed[(firstLineEnd + 1)..].Trim();
                if (trimmed.EndsWith("```", StringComparison.Ordinal))
                    trimmed = trimmed[..^3].Trim();
            }

            var start = trimmed.IndexOf('{');
            var end = trimmed.LastIndexOf('}');
            return start >= 0 && end > start
                ? trimmed[start..(end + 1)]
                : null;
        }

        private static string? ValidateActionContract(AiRiskActionPlan plan)
        {
            if (plan.Actions.Count > 10)
                return "actions must contain at most 10 entries.";
            foreach (var action in plan.Actions)
            {
                if (action.Priority is < 1 or > 5)
                    return "action priority must be between 1 and 5.";
                if (string.IsNullOrWhiteSpace(action.Title) || action.Title!.Trim().Length > 200)
                    return "action title is required and must not exceed 200 characters.";
                if (action.Detail?.Trim().Length > 2000)
                    return "action detail must not exceed 2000 characters.";
                if (string.IsNullOrWhiteSpace(action.OwnerRole) || !ActionOwnerRoles.Contains(action.OwnerRole.Trim()))
                    return "action ownerRole must be PM or WAREHOUSE_MANAGER.";
                if (action.RelatedRiskTypes == null || action.RelatedRiskTypes.Count == 0 ||
                    action.RelatedRiskTypes.Any(t => string.IsNullOrWhiteSpace(t) || !KnownRiskTypes.Contains(t.Trim())))
                    return "each action must reference at least one known risk type.";
            }
            return null;
        }

        private static List<RiskItemResponse> DetectScheduleRisks(List<TaskItem> openTasks, DateTime today)
        {
            var risks = new List<RiskItemResponse>();
            foreach (var task in openTasks)
            {
                var durationDays = Math.Max(1, (task.BaselineEnd.Date - task.BaselineStart.Date).Days + 1);
                var elapsedDays = Math.Clamp((today - task.BaselineStart.Date).Days + 1, 0, durationDays);
                var expectedPct = durationDays <= 1 && today >= task.BaselineEnd.Date
                    ? 100
                    : Math.Round(100m * elapsedDays / durationDays, 2);
                if (today < task.BaselineStart.Date)
                    expectedPct = 0;
                var gap = expectedPct - task.ActualProgressPct;
                var daysToDeadline = (task.BaselineEnd.Date - today).Days;

                if (gap >= ProgressDeviationThresholdPct)
                {
                    risks.Add(new RiskItemResponse
                    {
                        RiskType = ProjectRiskTypes.ProgressDeviation,
                        Severity = RiskSeverity.Warning,
                        TaskId = task.TaskId,
                        TaskName = task.TaskName,
                        Message = $"Actual {task.TaskName} progress is {gap:0} percentage points below expected progress.",
                        Metrics = new Dictionary<string, decimal>
                        {
                            ["expectedPct"] = expectedPct,
                            ["actualPct"] = task.ActualProgressPct,
                            ["gapPct"] = Math.Round(gap, 2)
                        }
                    });
                }

                if (gap > ProgressGapTolerancePct && daysToDeadline <= DeadlineNearDays)
                {
                    var overdue = daysToDeadline < 0;
                    risks.Add(new RiskItemResponse
                    {
                        RiskType = ProjectRiskTypes.ScheduleDelay,
                        Severity = overdue ? RiskSeverity.Critical : RiskSeverity.Warning,
                        TaskId = task.TaskId,
                        TaskName = task.TaskName,
                        Message = overdue
                            ? $"{task.TaskName} is {-daysToDeadline} days past its planned date and {gap:0} points behind schedule."
                            : $"{task.TaskName} is {gap:0} points behind schedule with {daysToDeadline} days to its planned date.",
                        Metrics = new Dictionary<string, decimal>
                        {
                            ["gapPct"] = Math.Round(gap, 2),
                            ["daysToDeadline"] = daysToDeadline
                        }
                    });
                }

                var pace = elapsedDays > 0 ? task.ActualProgressPct / elapsedDays : 0;
                if (task.ActualProgressPct < 100)
                {
                    decimal? projectedEndOffset = pace > 0
                        ? Math.Ceiling((100 - task.ActualProgressPct) / pace)
                        : null;
                    if (projectedEndOffset.HasValue)
                    {
                        var projectedEnd = task.BaselineStart.Date.AddDays(elapsedDays - 1 + (double)projectedEndOffset.Value);
                        if (projectedEnd > task.BaselineEnd.Date)
                        {
                            risks.Add(new RiskItemResponse
                            {
                                RiskType = ProjectRiskTypes.WorkItemDelay,
                                Severity = RiskSeverity.Warning,
                                TaskId = task.TaskId,
                                TaskName = task.TaskName,
                                Message = $"{task.TaskName} may not finish before the planned date of {task.BaselineEnd:yyyy-MM-dd} at its current pace.",
                                Metrics = new Dictionary<string, decimal>
                                {
                                    ["actualPct"] = task.ActualProgressPct,
                                    ["pacePerDay"] = Math.Round(pace, 2)
                                }
                            });
                        }
                    }
                    else if (today >= task.BaselineStart.Date && daysToDeadline <= DeadlineNearDays * 2)
                    {
                        risks.Add(new RiskItemResponse
                        {
                            RiskType = ProjectRiskTypes.WorkItemDelay,
                            Severity = RiskSeverity.Warning,
                            TaskId = task.TaskId,
                            TaskName = task.TaskName,
                            Message = $"{task.TaskName} has not progressed and may not finish before the planned date of {task.BaselineEnd:yyyy-MM-dd}.",
                            Metrics = new Dictionary<string, decimal>
                            {
                                ["actualPct"] = task.ActualProgressPct,
                                ["daysToDeadline"] = daysToDeadline
                            }
                        });
                    }
                }
            }
            return risks;
        }

        private async Task<List<RiskItemResponse>> DetectMaterialRisksAsync(
            int projectId, List<TaskItem> openTasks, DateTime today)
        {
            var risks = new List<RiskItemResponse>();
            if (openTasks.Count == 0)
                return risks;

            var taskIds = openTasks.Select(t => t.TaskId).ToList();
            var taskById = openTasks.ToDictionary(t => t.TaskId);
            var requirements = await _uow.TaskMaterialRequirements.GetAllAsync(r => taskIds.Contains(r.TaskId));
            if (requirements.Count == 0)
                return risks;

            var requisitions = await _uow.MaterialRequisitions.GetAllAsync(r =>
                r.MaterialRequest.ProjectId == projectId && r.IssuedQuantity > 0,
                query => query.Include(r => r.MaterialRequest));
            var issuedByTaskVariant = requisitions
                .Where(r => r.MaterialRequest.TaskId.HasValue)
                .GroupBy(r => (TaskId: r.MaterialRequest.TaskId!.Value, r.VariantId))
                .ToDictionary(g => g.Key, g => g.Sum(r => r.IssuedQuantity));
            var returns = await _uow.MaterialReturns.GetAllAsync(r =>
                r.MaterialRequest.ProjectId == projectId,
                query => query.Include(r => r.MaterialRequest));
            var returnedByTaskVariant = returns
                .Where(r => r.MaterialRequest.TaskId.HasValue)
                .GroupBy(r => (TaskId: r.MaterialRequest.TaskId!.Value, r.VariantId))
                .ToDictionary(g => g.Key, g => g.Sum(r => r.Quantity));

            var remainingByVariant = new Dictionary<int, decimal>();
            var remainingByTaskVariant = new Dictionary<(int TaskId, int VariantId), decimal>();
            foreach (var requirement in requirements)
            {
                if (!taskById.ContainsKey(requirement.TaskId))
                    continue;
                var key = (requirement.TaskId, requirement.VariantId);
                var netIssued = Math.Max(0,
                    issuedByTaskVariant.GetValueOrDefault(key) - returnedByTaskVariant.GetValueOrDefault(key));
                var remaining = Math.Max(0, requirement.GrossQuantityRequired - netIssued);
                if (remaining <= 0)
                    continue;
                remainingByTaskVariant[key] = remainingByTaskVariant.GetValueOrDefault(key) + remaining;
                remainingByVariant[requirement.VariantId] = remainingByVariant.GetValueOrDefault(requirement.VariantId) + remaining;
            }
            if (remainingByVariant.Count == 0)
                return risks;

            var warehouse = await _warehouseContext.GetActiveWarehouseAsync();
            var variantIds = remainingByVariant.Keys.ToList();
            var inventories = warehouse == null
                ? new List<InventoryRecord>()
                : await _uow.Inventories.GetAllAsync(i =>
                    i.WarehouseId == warehouse.WarehouseId && variantIds.Contains(i.VariantId));
            var availableByVariant = inventories.ToDictionary(
                i => i.VariantId,
                i => Math.Max(0, i.QuantityOnHand - i.ReservedQuantity - i.QuarantineQuantity));

            foreach (var (variantId, remaining) in remainingByVariant)
            {
                var available = availableByVariant.GetValueOrDefault(variantId);
                if (remaining > available)
                {
                    risks.Add(new RiskItemResponse
                    {
                        RiskType = ProjectRiskTypes.MaterialShortage,
                        Severity = available <= 0 ? RiskSeverity.Critical : RiskSeverity.Warning,
                        VariantId = variantId,
                        Message = $"Material stock covers only {available:0.##} of the remaining {remaining:0.##} required units for variant {variantId}.",
                        Metrics = new Dictionary<string, decimal>
                        {
                            ["remainingRequired"] = remaining,
                            ["available"] = available,
                            ["shortfall"] = remaining - available
                        }
                    });
                }
            }

            var requests = await _uow.MaterialRequests.GetAllAsync(r => r.ProjectId == projectId);
            var unfulfilledTaskIds = new HashSet<int>(requests
                .Where(r => r.TaskId.HasValue && r.Status is MaterialRequestStatuses.Pending
                    or MaterialRequestStatuses.Approved or MaterialRequestStatuses.PartiallyApproved)
                .Select(r => r.TaskId!.Value));
            var tasksWithRequests = new HashSet<int>(requests
                .Where(r => r.TaskId.HasValue)
                .Select(r => r.TaskId!.Value));
            foreach (var task in openTasks)
            {
                if (task.BaselineStart.Date > today.AddDays(AvailabilityWindowDays))
                    continue;
                var hasRemaining = remainingByTaskVariant.Keys.Any(k => k.TaskId == task.TaskId);
                if (!hasRemaining)
                    continue;
                if (!tasksWithRequests.Contains(task.TaskId) || unfulfilledTaskIds.Contains(task.TaskId))
                {
                    risks.Add(new RiskItemResponse
                    {
                        RiskType = ProjectRiskTypes.MaterialAvailability,
                        Severity = RiskSeverity.Warning,
                        TaskId = task.TaskId,
                        TaskName = task.TaskName,
                        Message = $"Required material for {task.TaskName} has not been supplied before work starts on {task.BaselineStart:yyyy-MM-dd}.",
                        Metrics = new Dictionary<string, decimal>
                        {
                            ["daysToStart"] = (task.BaselineStart.Date - today).Days
                        }
                    });
                }
            }

            return risks;
        }

        private static List<RiskItemResponse> DetectBudgetRisks(Project project, List<TaskItem> tasks, decimal spend)
        {
            var risks = new List<RiskItemResponse>();
            if (project.TotalProjectBudget > 0)
            {
                var ratio = spend / project.TotalProjectBudget;
                if (ratio >= BudgetCriticalRatio)
                {
                    risks.Add(new RiskItemResponse
                    {
                        RiskType = ProjectRiskTypes.BudgetRisk,
                        Severity = RiskSeverity.Critical,
                        Message = $"Material spending of {spend:0.##} has reached the allocated budget of {project.TotalProjectBudget:0.##}.",
                        Metrics = new Dictionary<string, decimal>
                        {
                            ["spent"] = spend,
                            ["budget"] = project.TotalProjectBudget,
                            ["ratio"] = Math.Round(ratio, 4)
                        }
                    });
                }
                else if (ratio >= BudgetWarnRatio)
                {
                    risks.Add(new RiskItemResponse
                    {
                        RiskType = ProjectRiskTypes.BudgetRisk,
                        Severity = RiskSeverity.Warning,
                        Message = $"Material spending of {spend:0.##} is approaching the allocated budget of {project.TotalProjectBudget:0.##}.",
                        Metrics = new Dictionary<string, decimal>
                        {
                            ["spent"] = spend,
                            ["budget"] = project.TotalProjectBudget,
                            ["ratio"] = Math.Round(ratio, 4)
                        }
                    });
                }
            }
            foreach (var task in tasks.Where(t =>
                t.Status is not (DomainTaskStatus.CANCELLED or DomainTaskStatus.REJECTED) && t.ActualCost > t.PlannedBudget))
            {
                risks.Add(new RiskItemResponse
                {
                    RiskType = ProjectRiskTypes.BudgetRisk,
                    Severity = RiskSeverity.Warning,
                    TaskId = task.TaskId,
                    TaskName = task.TaskName,
                    Message = $"Actual cost of {task.TaskName} exceeds its planned budget.",
                    Metrics = new Dictionary<string, decimal>
                    {
                        ["planned"] = task.PlannedBudget,
                        ["actual"] = task.ActualCost,
                        ["overrun"] = task.ActualCost - task.PlannedBudget
                    }
                });
            }
            return risks;
        }

        private static List<RiskItemResponse> DetectOverallDelay(
            Project project, List<TaskItem> tasks, List<RiskItemResponse> risks, DateTime today)
        {
            var output = new List<RiskItemResponse>();
            var criticals = risks.Count(r => r.Severity == RiskSeverity.Critical);
            var warnings = risks.Count(r => r.Severity == RiskSeverity.Warning);
            var latestTaskEnd = tasks
                .Where(t => t.Status is not (DomainTaskStatus.CANCELLED or DomainTaskStatus.REJECTED))
                .Select(t => (DateTime?)t.BaselineEnd.Date)
                .DefaultIfEmpty()
                .Max();
            if (criticals > 0 || warnings >= 2 || (latestTaskEnd.HasValue && latestTaskEnd > project.BaselineEnd.Date && tasks.Count > 0))
            {
                output.Add(new RiskItemResponse
                {
                    RiskType = ProjectRiskTypes.OverallProjectDelay,
                    Severity = criticals > 0 ? RiskSeverity.Critical : RiskSeverity.Warning,
                    Message = criticals > 0
                        ? $"Current delays may affect the project completion date of {project.BaselineEnd:yyyy-MM-dd}."
                        : $"Several warnings are active; review whether the completion date of {project.BaselineEnd:yyyy-MM-dd} still holds.",
                    Metrics = new Dictionary<string, decimal>
                    {
                        ["criticalCount"] = criticals,
                        ["warningCount"] = warnings
                    }
                });
            }
            return output;
        }
    }
}
