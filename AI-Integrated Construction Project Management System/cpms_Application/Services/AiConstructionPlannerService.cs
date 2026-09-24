using ClosedXML.Excel;
using cpms_Application.Interfaces;
using cpms_Application.Request.AiConstructionPlanner;
using cpms_Application.Response;
using cpms_Application.Response.AiConstructionPlanner;
using cpms_Application.Utilities;
using cpms_Domain.Models;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.AspNetCore.Http;
using WordXml = DocumentFormat.OpenXml.Wordprocessing;
using System.Net;
using System.Text;
using System.Text.Json;

using DomainTaskStatus = cpms_Domain.Models.TaskStatus;

namespace cpms_Application.Services
{
    public class AiConstructionPlannerService : IAiConstructionPlannerService
    {
        private const string PlannerVersion = "1.0";
        private const int MaxLongAnswerLength = 2000;
        private const int MaxShortAnswerLength = 1000;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly IUnitOfWork _uow;
        private readonly IClaimService _claimService;
        private readonly IProjectAccessService _projectAccess;
        private readonly IGoogleAIClient _googleAIClient;

        public AiConstructionPlannerService(
            IUnitOfWork uow,
            IClaimService claimService,
            IGoogleAIClient googleAIClient,
            IProjectAccessService? projectAccess = null)
        {
            _uow = uow;
            _claimService = claimService;
            _projectAccess = projectAccess ?? new ProjectAccessService(uow, claimService);
            _googleAIClient = googleAIClient;
        }

        public Task<ApiResponse> GetQuestionsAsync()
        {
            return Task.FromResult(new ApiResponse().SetOk(new ConstructionPlannerQuestionsResponse
            {
                Version = PlannerVersion,
                Questions = BuildQuestions()
            }));
        }

        public async Task<ApiResponse> GeneratePlanJsonAsync(GenerateConstructionPlanRequest request)
        {
            var validationError = ValidateRequest(request);
            if (validationError != null)
                return validationError;

            Project? project = null;
            if (request.ProjectId.HasValue)
            {
                project = await _uow.Projects.GetByIdAsync(request.ProjectId.Value);
                if (project == null)
                    return new ApiResponse().SetNotFound("Project not found.");

                if (!await _projectAccess.CanViewProjectAsStaffAsync(project))
                    return new ApiResponse().SetApiResponse(HttpStatusCode.Forbidden, false, "You do not have access to this project.");
            }

            var (error, plan) = await GeneratePlanCoreAsync(request.Answers, project);
            if (error != null)
                return error;

            return new ApiResponse().SetOk(plan);
        }

        private async Task<(ApiResponse? Error, ConstructionPlanJsonResponse? Plan)> GeneratePlanCoreAsync(
            ConstructionPlanAnswersRequest? answers, Project? project, string? completionContext = null)
        {
            var validationError = ValidateAnswers(answers);
            if (validationError != null)
                return (validationError, null);

            var normalized = NormalizeAnswers(answers!);
            return await CallPlannerAiAsync(BuildPrompt(normalized, project, completionContext));
        }

        private async Task<(ApiResponse? Error, ConstructionPlanJsonResponse? Plan)> GeneratePlanCoreFromBriefAsync(
            AiProjectBriefRequest? brief, Project? project, string? completionContext = null)
        {
            var validationError = ValidateBrief(brief);
            if (validationError != null)
                return (validationError, null);

            return await CallPlannerAiAsync(BuildPromptFromBrief(brief!, project, completionContext));
        }

        private async Task<(ApiResponse? Error, ConstructionPlanJsonResponse? Plan)> CallPlannerAiAsync(string prompt)
        {
            var systemInstruction = BuildSystemInstruction();
            var aiResult = await _googleAIClient.GenerateTextAsync(systemInstruction, prompt);

            if (!aiResult.IsSuccess)
            {
                if (aiResult.IsRateLimited)
                {
                    return (new ApiResponse().SetApiResponse(
                        HttpStatusCode.TooManyRequests,
                        false,
                        "Gemini rate limit exceeded. Wait a minute and try again.",
                        new { errorCode = "GEMINI_RATE_LIMITED" }), null);
                }

                return (new ApiResponse().SetBadRequest(aiResult.ErrorMessage ?? "AI request failed."), null);
            }

            if (!TryParsePlan(aiResult.Text, out var plan, out var parseError))
            {
                return (new ApiResponse().SetBadRequest(
                    new { errorCode = "AI_JSON_INVALID", detail = parseError },
                    "AI returned invalid planner JSON. Please try again."), null);
            }

            NormalizePlan(plan!);

            var contractError = ValidatePlanContract(plan!);
            if (contractError != null)
            {
                return (new ApiResponse().SetBadRequest(
                    new { errorCode = "AI_JSON_CONTRACT_INVALID", detail = contractError },
                    "AI returned planner JSON that does not match the expected Excel plan contract."), null);
            }

            return (null, plan);
        }

        public async Task<ApiResponse> GenerateExcelAsync(GenerateConstructionPlanExcelRequest request)
        {
            if (request?.Plan == null)
            {
                return PlannerBadRequest(
                    "A generated construction plan JSON payload is required.",
                    "PLANNER_JSON_REQUIRED");
            }

            if (request.ProjectId.HasValue)
            {
                var project = await _uow.Projects.GetByIdAsync(request.ProjectId.Value);
                if (project == null)
                    return new ApiResponse().SetNotFound("Project not found.");
                if (!await _projectAccess.CanViewProjectAsStaffAsync(project))
                    return new ApiResponse().SetApiResponse(HttpStatusCode.Forbidden, false, "You do not have access to this project.");
            }

            NormalizePlan(request.Plan);
            var contractError = ValidatePlanContract(request.Plan);
            if (contractError != null)
            {
                return new ApiResponse().SetBadRequest(
                    new { errorCode = "PLANNER_JSON_CONTRACT_INVALID", detail = contractError },
                    "Planner JSON does not match the expected Excel plan contract.");
            }

            using var workbook = new XLWorkbook();
            ExcelSheetWriter.AddSheet(workbook, "Overview", request.Plan.ExcelSheets.Overview);
            ExcelSheetWriter.AddSheet(workbook, "Phases", request.Plan.ExcelSheets.Phases);
            ExcelSheetWriter.AddSheet(workbook, "Tasks", request.Plan.ExcelSheets.Tasks);
            ExcelSheetWriter.AddSheet(workbook, "Materials", request.Plan.ExcelSheets.Materials);
            ExcelSheetWriter.AddSheet(workbook, "Labor", request.Plan.ExcelSheets.Labor);
            ExcelSheetWriter.AddSheet(workbook, "Equipment", request.Plan.ExcelSheets.Equipment);
            ExcelSheetWriter.AddSheet(workbook, "Cost Plan", request.Plan.ExcelSheets.CostPlan);
            ExcelSheetWriter.AddSheet(workbook, "Procurement Plan", request.Plan.ExcelSheets.ProcurementPlan);
            ExcelSheetWriter.AddSheet(workbook, "Risk Register", request.Plan.ExcelSheets.RiskRegister);
            ExcelSheetWriter.AddSheet(workbook, "Permit Checklist", request.Plan.ExcelSheets.PermitChecklist);
            ExcelSheetWriter.AddSheet(workbook, "Safety Plan", request.Plan.ExcelSheets.SafetyPlan);
            ExcelSheetWriter.AddSheet(workbook, "Milestones", request.Plan.ExcelSheets.Milestones);

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);

            var file = new ConstructionPlanExcelFileResponse
            {
                Content = stream.ToArray(),
                FileName = BuildDownloadFileName(request.FileName, request.Plan)
            };

            return new ApiResponse().SetOk(file);
        }

        public async Task<ApiResponse> GenerateProjectPhasesPreviewAsync(int projectId, GenerateProjectAiPhasesRequest request)
        {
            var project = await _uow.Projects.GetByIdAsync(projectId);
            if (project == null)
                return new ApiResponse().SetNotFound("Project not found.");
            if (!_projectAccess.IsOwningProjectManager(project))
                return new ApiResponse().SetApiResponse(HttpStatusCode.Forbidden, false,
                    "Only the owning project manager may use AI planning for this project.");

            var (error, plan) = request?.Brief != null
                ? await GeneratePlanCoreFromBriefAsync(request.Brief, project)
                : await GeneratePlanCoreAsync(request?.Answers, project);
            if (error != null)
                return error;

            var usedTemps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var preview = new ProjectAiPlanPreviewResponse
            {
                Phases = plan!.ExcelSheets.Phases
                    .Select((row, index) => MapPhaseProposal(project, row, index, usedTemps))
                    .ToList()
            };
            return new ApiResponse().SetOk(preview);
        }

        public async Task<ApiResponse> GenerateProjectTasksPreviewAsync(int projectId, GenerateProjectAiTasksRequest request)
        {
            var project = await _uow.Projects.GetByIdAsync(projectId);
            if (project == null)
                return new ApiResponse().SetNotFound("Project not found.");
            if (!_projectAccess.IsOwningProjectManager(project))
                return new ApiResponse().SetApiResponse(HttpStatusCode.Forbidden, false,
                    "Only the owning project manager may use AI planning for this project.");
            if (request == null || (!request.PhaseId.HasValue && (request.Phases == null || request.Phases.Count == 0)))
                return new ApiResponse().SetBadRequest(
                    "Provide either an existing phaseId or the phase preview from phases:generate.");

            Phase? selectedPhase = null;
            if (request.PhaseId.HasValue)
            {
                selectedPhase = await _uow.Phases.GetByIdAsync(request.PhaseId.Value);
                if (selectedPhase == null || selectedPhase.ProjectId != projectId)
                    return new ApiResponse().SetNotFound("Phase not found in this project.");
            }

            var (error, plan) = request.Brief != null
                ? await GeneratePlanCoreFromBriefAsync(request.Brief, project)
                : await GeneratePlanCoreAsync(request.Answers, project);
            if (error != null)
                return error;

            var preview = new ProjectAiPlanPreviewResponse();
            var usedTemps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var usedTaskTemps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (selectedPhase != null)
            {
                var matchKey = FindMatchingAiPhaseKey(plan!, selectedPhase.Name);
                if (matchKey == null)
                    return new ApiResponse().SetBadRequest(
                        $"No generated tasks match phase '{selectedPhase.Name}'. Re-run phases:generate first.");
                preview.Phases.Add(new AiPhaseProposalResponse
                {
                    TempId = string.Empty,
                    AiKey = string.Empty,
                    PhaseId = selectedPhase.PhaseId,
                    Name = selectedPhase.Name,
                    Description = selectedPhase.Description,
                    SequenceOrder = selectedPhase.SequenceOrder,
                    BaselineStart = selectedPhase.BaselineStart,
                    BaselineEnd = selectedPhase.BaselineEnd
                });
                foreach (var (row, index) in plan!.ExcelSheets.Tasks
                    .Select((row, index) => (row, index))
                    .Where(x => string.Equals((x.row.PhaseId ?? string.Empty).Trim(), matchKey, StringComparison.OrdinalIgnoreCase)))
                {
                    preview.Tasks.Add(MapTaskProposal(project, row, index,
                        UniqueTempId($"TSK-{(string.IsNullOrWhiteSpace(row.TaskId) ? $"T{index + 1:000}" : row.TaskId.Trim())}", usedTaskTemps),
                        null, selectedPhase.PhaseId, selectedPhase.BaselineStart, selectedPhase.BaselineEnd));
                }
                if (preview.Tasks.Count == 0)
                    return new ApiResponse().SetBadRequest(
                        $"No generated tasks match phase '{selectedPhase.Name}'. Re-run phases:generate first.");
                return new ApiResponse().SetOk(preview);
            }

            var echo = request.Phases!
                .Where(p => p != null && !string.IsNullOrWhiteSpace(p.TempId))
                .ToList();
            if (echo.Count == 0)
                return new ApiResponse().SetBadRequest("Phase preview entries must carry temporary IDs.");
            var tempByAiKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in echo)
            {
                var temp = UniqueTempId(item.TempId.Trim(), usedTemps);
                var aiKey = (item.AiKey ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(aiKey) && !tempByAiKey.ContainsKey(aiKey))
                    tempByAiKey[aiKey] = temp;
                preview.Phases.Add(new AiPhaseProposalResponse
                {
                    TempId = temp,
                    AiKey = aiKey,
                    Name = string.IsNullOrWhiteSpace(item.Name) ? temp : item.Name.Trim(),
                    Description = string.IsNullOrWhiteSpace(item.Description) ? null : item.Description.Trim(),
                    SequenceOrder = item.SequenceOrder,
                    BaselineStart = item.BaselineStart,
                    BaselineEnd = item.BaselineEnd
                });
            }

            var phaseNameByAiKey = plan!.ExcelSheets.Phases
                .Where(p => !string.IsNullOrWhiteSpace(p.PhaseId))
                .GroupBy(p => p.PhaseId.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().PhaseName ?? string.Empty, StringComparer.OrdinalIgnoreCase);
            foreach (var (row, index) in plan.ExcelSheets.Tasks.Select((row, index) => (row, index)))
            {
                var key = (row.PhaseId ?? string.Empty).Trim();
                string? phaseTemp = null;
                if (!tempByAiKey.TryGetValue(key, out phaseTemp))
                {
                    phaseNameByAiKey.TryGetValue(key, out var aiPhaseName);
                    var byName = preview.Phases.FirstOrDefault(p =>
                        !string.IsNullOrWhiteSpace(aiPhaseName) &&
                        string.Equals(p.Name, aiPhaseName.Trim(), StringComparison.OrdinalIgnoreCase));
                    phaseTemp = byName?.TempId;
                }
                if (phaseTemp == null)
                {
                    var freshRow = plan.ExcelSheets.Phases.FirstOrDefault(p =>
                        string.Equals((p.PhaseId ?? string.Empty).Trim(), key, StringComparison.OrdinalIgnoreCase));
                    if (freshRow != null)
                    {
                        var extra = MapPhaseProposal(project, freshRow, preview.Phases.Count, usedTemps);
                        tempByAiKey[key] = extra.TempId;
                        preview.Phases.Add(extra);
                        phaseTemp = extra.TempId;
                    }
                    else
                    {
                        phaseTemp = preview.Phases[0].TempId;
                    }
                }
                var target = preview.Phases.First(p => string.Equals(p.TempId, phaseTemp, StringComparison.OrdinalIgnoreCase));
                preview.Tasks.Add(MapTaskProposal(project, row, index,
                    UniqueTempId($"TSK-{(string.IsNullOrWhiteSpace(row.TaskId) ? $"T{index + 1:000}" : row.TaskId.Trim())}", usedTaskTemps),
                    phaseTemp, null, target.BaselineStart, target.BaselineEnd));
            }

            return new ApiResponse().SetOk(preview);
        }

        public async Task<ApiResponse> CompleteProjectAiPlanAsync(int projectId, CompleteProjectAiPlanRequest request)
        {
            var project = await _uow.Projects.GetByIdAsync(projectId);
            if (project == null)
                return new ApiResponse().SetNotFound("Project not found.");
            if (!_projectAccess.IsOwningProjectManager(project))
                return new ApiResponse().SetApiResponse(HttpStatusCode.Forbidden, false,
                    "Only the owning project manager may use AI planning for this project.");
            if (request == null || (request.Brief == null && request.Answers == null))
                return new ApiResponse().SetBadRequest("Provide either a project brief or planning answers.");
            if (Length(request.FocusNote) > MaxLongAnswerLength)
                return new ApiResponse().SetBadRequest("Focus note is too long.");

            var existingPhases = (await _uow.Phases.GetAllAsync(p => p.ProjectId == projectId))
                .OrderBy(p => p.SequenceOrder).ThenBy(p => p.Name).ToList();
            var existingTasks = (await _uow.TaskItems.GetAllAsync(t => t.ProjectId == projectId))
                .OrderBy(t => t.TaskId).ToList();

            var completionContext = BuildCompletionContext(existingPhases, existingTasks, request.FocusNote);
            var (error, plan) = request.Brief != null
                ? await GeneratePlanCoreFromBriefAsync(request.Brief, project, completionContext)
                : await GeneratePlanCoreAsync(request.Answers, project, completionContext);
            if (error != null)
                return error;

            var preview = new ProjectAiPlanPreviewResponse();
            var usedTemps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var usedTaskTemps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var existingPhaseByName = existingPhases
                .GroupBy(p => p.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            var existingTaskNamesByPhase = existingTasks
                .GroupBy(t => t.PhaseId)
                .ToDictionary(
                    g => g.Key,
                    g => new HashSet<string>(g.Select(t => t.TaskName), StringComparer.OrdinalIgnoreCase));
            var tempByAiKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var existingIdByAiKey = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var phaseNameByAiKey = plan!.ExcelSheets.Phases
                .Where(p => !string.IsNullOrWhiteSpace(p.PhaseId))
                .GroupBy(p => p.PhaseId.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().PhaseName ?? string.Empty, StringComparer.OrdinalIgnoreCase);

            foreach (var (row, index) in plan.ExcelSheets.Phases.Select((row, index) => (row, index)))
            {
                var aiKey = string.IsNullOrWhiteSpace(row.PhaseId) ? $"P{index + 1:00}" : row.PhaseId.Trim();
                var name = string.IsNullOrWhiteSpace(row.PhaseName) ? $"Phase {index + 1}" : row.PhaseName.Trim();
                if (existingPhaseByName.TryGetValue(name, out var existing))
                {
                    if (!existingIdByAiKey.ContainsKey(aiKey))
                        existingIdByAiKey[aiKey] = existing.PhaseId;
                    preview.Warnings.Add($"Phase '{name}' already exists and was skipped; generated tasks target the existing phase.");
                    continue;
                }
                var proposal = MapPhaseProposal(project, row, index, usedTemps);
                tempByAiKey[proposal.AiKey] = proposal.TempId;
                preview.Phases.Add(proposal);
            }

            var seenNewTaskNames = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var (row, index) in plan.ExcelSheets.Tasks.Select((row, index) => (row, index)))
            {
                var taskName = string.IsNullOrWhiteSpace(row.TaskName) ? $"Task {index + 1}" : row.TaskName.Trim();
                var key = (row.PhaseId ?? string.Empty).Trim();
                string? phaseTemp = null;
                int? phaseId = null;
                if (!tempByAiKey.TryGetValue(key, out phaseTemp))
                {
                    if (!existingIdByAiKey.TryGetValue(key, out var mappedId))
                    {
                        phaseNameByAiKey.TryGetValue(key, out var aiPhaseName);
                        var candidate = !string.IsNullOrWhiteSpace(aiPhaseName)
                            ? preview.Phases.FirstOrDefault(p => string.Equals(p.Name, aiPhaseName.Trim(), StringComparison.OrdinalIgnoreCase))
                            : null;
                        if (candidate != null)
                        {
                            phaseTemp = candidate.TempId;
                        }
                        else
                        {
                            var existing = !string.IsNullOrWhiteSpace(aiPhaseName) && existingPhaseByName.TryGetValue(aiPhaseName.Trim(), out var byAiName)
                                ? byAiName
                                : existingPhaseByName.TryGetValue(key, out var byKey) ? byKey : null;
                            if (existing == null)
                            {
                                preview.Warnings.Add($"Task '{taskName}' referenced unknown phase '{key}' and was skipped.");
                                continue;
                            }
                            phaseId = existing.PhaseId;
                            existingIdByAiKey[key] = existing.PhaseId;
                        }
                    }
                    else
                    {
                        phaseId = mappedId;
                    }
                }

                string targetKey;
                string targetName;
                DateTime windowStart;
                DateTime windowEnd;
                if (phaseTemp != null)
                {
                    var target = preview.Phases.First(p => string.Equals(p.TempId, phaseTemp, StringComparison.OrdinalIgnoreCase));
                    targetKey = "T:" + phaseTemp;
                    targetName = target.Name;
                    windowStart = target.BaselineStart;
                    windowEnd = target.BaselineEnd;
                }
                else
                {
                    var target = existingPhases.First(p => p.PhaseId == phaseId!.Value);
                    targetKey = "P:" + target.PhaseId;
                    targetName = target.Name;
                    windowStart = target.BaselineStart;
                    windowEnd = target.BaselineEnd;
                }
                if (!seenNewTaskNames.TryGetValue(targetKey, out var seen))
                {
                    seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    seenNewTaskNames[targetKey] = seen;
                }
                var duplicate = !seen.Add(taskName) ||
                    (phaseId.HasValue && existingTaskNamesByPhase.TryGetValue(phaseId.Value, out var taken) && taken.Contains(taskName));
                if (duplicate)
                {
                    preview.Warnings.Add($"Task '{taskName}' already exists in phase '{targetName}' and was skipped.");
                    continue;
                }
                preview.Tasks.Add(MapTaskProposal(project, row, index,
                    UniqueTempId($"TSK-{(string.IsNullOrWhiteSpace(row.TaskId) ? $"T{index + 1:000}" : row.TaskId.Trim())}", usedTaskTemps),
                    phaseTemp, phaseId, windowStart, windowEnd));
            }

            foreach (var existingId in existingIdByAiKey.Values.Distinct().OrderBy(id => id))
            {
                if (preview.Tasks.Any(t => t.PhaseId == existingId) &&
                    preview.Phases.All(p => p.PhaseId != existingId))
                {
                    var existing = existingPhases.First(p => p.PhaseId == existingId);
                    preview.Phases.Add(new AiPhaseProposalResponse
                    {
                        TempId = string.Empty,
                        AiKey = string.Empty,
                        PhaseId = existing.PhaseId,
                        Name = existing.Name,
                        Description = existing.Description,
                        SequenceOrder = existing.SequenceOrder,
                        BaselineStart = existing.BaselineStart,
                        BaselineEnd = existing.BaselineEnd
                    });
                }
            }

            return new ApiResponse().SetOk(preview);
        }

        private static string BuildCompletionContext(
            List<Phase> existingPhases, List<TaskItem> existingTasks, string? focusNote)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Completing an existing plan: propose ONLY phases and tasks that do not duplicate the current state below.");
            if (existingPhases.Count == 0)
            {
                builder.AppendLine("The project has no phases or tasks yet; propose the full remaining plan.");
            }
            else
            {
                builder.AppendLine("Current phases (name | baseline):");
                foreach (var phase in existingPhases)
                    builder.AppendLine($"- {phase.Name} ({phase.BaselineStart:yyyy-MM-dd} to {phase.BaselineEnd:yyyy-MM-dd})");
                var tasksByPhase = existingTasks.GroupBy(t => t.PhaseId).ToList();
                if (tasksByPhase.Count > 0)
                {
                    builder.AppendLine("Current tasks (phase | task | baseline | budget):");
                    foreach (var group in tasksByPhase)
                    {
                        var phaseName = existingPhases.FirstOrDefault(p => p.PhaseId == group.Key)?.Name ?? $"Phase {group.Key}";
                        foreach (var task in group)
                            builder.AppendLine($"- {phaseName} | {task.TaskName} ({task.BaselineStart:yyyy-MM-dd} to {task.BaselineEnd:yyyy-MM-dd}, budget {task.PlannedBudget})");
                    }
                }
            }
            if (!string.IsNullOrWhiteSpace(focusNote))
                builder.AppendLine($"PM focus: {focusNote.Trim()}");
            builder.AppendLine("Rules: reuse the exact existing phase names above when a generated task belongs to an existing phase; every other task must reference a newly proposed phase; keep all dates inside the project baseline.");
            return builder.ToString();
        }

        public async Task<ApiResponse> ConfirmProjectAiPlanAsync(int projectId, ConfirmProjectAiPlanRequest request)
        {
            var pmId = _claimService.GetUserClaim().Id;
            var project = await _uow.Projects.GetByIdAsync(projectId);
            if (project == null)
                return new ApiResponse().SetNotFound("Project not found.");
            if (!_projectAccess.IsOwningProjectManager(project))
                return new ApiResponse().SetApiResponse(HttpStatusCode.Forbidden, false,
                    "Only the owning project manager may confirm an AI plan for this project.");
            if (project.Status is ProjectStatus.COMPLETED or ProjectStatus.CANCELLED)
                return new ApiResponse().SetConflict("Closed projects cannot accept AI-planned phases or tasks.");

            var phases = request?.Phases ?? new List<AiPhaseProposalRequest>();
            var tasks = request?.Tasks ?? new List<AiTaskProposalRequest>();
            if (phases.Count == 0 && tasks.Count == 0)
                return new ApiResponse().SetBadRequest("At least one proposed phase or task is required.");

            var phaseByTemp = new Dictionary<string, AiPhaseProposalRequest>(StringComparer.OrdinalIgnoreCase);
            foreach (var phase in phases)
            {
                if (phase == null || string.IsNullOrWhiteSpace(phase.TempId))
                    return new ApiResponse().SetBadRequest("Every proposed phase must carry a temporary ID.");
                if (!phaseByTemp.TryAdd(phase.TempId.Trim(), phase))
                    return new ApiResponse().SetBadRequest($"Duplicate phase temporary ID '{phase.TempId.Trim()}'.");
            }
            var taskTemps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var task in tasks)
            {
                if (task == null || string.IsNullOrWhiteSpace(task.TempId))
                    return new ApiResponse().SetBadRequest("Every proposed task must carry a temporary ID.");
                if (!taskTemps.Add(task.TempId.Trim()))
                    return new ApiResponse().SetBadRequest($"Duplicate task temporary ID '{task.TempId.Trim()}'.");
                var hasTemp = !string.IsNullOrWhiteSpace(task.PhaseTempId);
                var hasId = task.PhaseId.HasValue && task.PhaseId.Value > 0;
                if (hasTemp == hasId)
                    return new ApiResponse().SetBadRequest(
                        $"Task '{task.TempId.Trim()}' must reference exactly one of phaseTempId or phaseId.");
                if (hasTemp && !phaseByTemp.ContainsKey(task.PhaseTempId!.Trim()))
                    return new ApiResponse().SetBadRequest(
                        $"Task '{task.TempId.Trim()}' references unknown phase '{task.PhaseTempId!.Trim()}'.");
            }

            var requestedExistingIds = tasks
                .Where(t => t.PhaseId.HasValue && t.PhaseId.Value > 0)
                .Select(t => t.PhaseId!.Value)
                .Distinct()
                .ToList();
            var existingPhases = requestedExistingIds.Count == 0
                ? new List<Phase>()
                : await _uow.Phases.GetAllAsync(p => p.ProjectId == projectId && requestedExistingIds.Contains(p.PhaseId));
            if (existingPhases.Count != requestedExistingIds.Count)
                return new ApiResponse().SetNotFound("One or more selected phases were not found in this project.");
            var existingById = existingPhases.ToDictionary(p => p.PhaseId);
            foreach (var existing in existingPhases)
            {
                if (existing.Status is PhaseStatus.COMPLETED or PhaseStatus.CANCELLED)
                    return new ApiResponse().SetConflict($"Phase '{existing.Name}' is closed and cannot accept new tasks.");
            }

            var takenPhaseNames = new HashSet<string>(
                (await _uow.Phases.GetAllAsync(p => p.ProjectId == projectId)).Select(p => p.Name),
                StringComparer.OrdinalIgnoreCase);
            var seenPhaseNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var phase in phases)
            {
                var name = (phase.Name ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
                    return new ApiResponse().SetBadRequest("Phase name is required and must not exceed 200 characters.");
                if (phase.Description?.Trim().Length > 2000)
                    return new ApiResponse().SetBadRequest("Phase description must not exceed 2000 characters.");
                if (phase.SequenceOrder < 0)
                    return new ApiResponse().SetBadRequest("Sequence order cannot be negative.");
                if (phase.BaselineEnd < phase.BaselineStart)
                    return new ApiResponse().SetBadRequest("Phase baseline dates are invalid.");
                if (phase.BaselineStart < project.BaselineStart || phase.BaselineEnd > project.BaselineEnd)
                    return new ApiResponse().SetBadRequest("Phase dates must stay inside the project baseline period.");
                if (!seenPhaseNames.Add(name))
                    return new ApiResponse().SetBadRequest($"Duplicate phase name '{name}'.");
                if (takenPhaseNames.Contains(name))
                    return new ApiResponse().SetConflict($"A phase named '{name}' already exists in the project.");
                if (phase.WorkCategoryId <= 0)
                    return new ApiResponse().SetBadRequest($"Phase '{name}' must reference a work category.");
            }

            var categoryById = new Dictionary<int, WorkCategory>();
            if (phases.Count > 0)
            {
                var categoryIds = phases.Select(p => p.WorkCategoryId).Distinct().ToList();
                foreach (var category in await _uow.WorkCategories.GetAllAsync(c => categoryIds.Contains(c.WorkCategoryId)))
                    categoryById[category.WorkCategoryId] = category;
                var missing = categoryIds.FirstOrDefault(id => !categoryById.ContainsKey(id));
                if (missing != 0)
                    return new ApiResponse().SetNotFound($"Work category {missing} was not found.");
            }

            var takenTaskNames = new Dictionary<int, HashSet<string>>();
            foreach (var existing in existingPhases)
            {
                var names = await _uow.TaskItems.GetAllAsync(t => t.PhaseId == existing.PhaseId);
                takenTaskNames[existing.PhaseId] = new HashSet<string>(names.Select(t => t.TaskName), StringComparer.OrdinalIgnoreCase);
            }
            var seenTaskNames = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var task in tasks)
            {
                var name = (task.TaskName ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
                    return new ApiResponse().SetBadRequest("Task name is required and must not exceed 200 characters.");
                if (task.PlannedBudget < 0)
                    return new ApiResponse().SetBadRequest("Task planned budget cannot be negative.");
                if (task.BaselineEnd < task.BaselineStart)
                    return new ApiResponse().SetBadRequest("Task baseline dates are invalid.");
                if (task.BaselineStart < project.BaselineStart || task.BaselineEnd > project.BaselineEnd)
                    return new ApiResponse().SetBadRequest("Task baseline dates must stay within the project baseline period.");

                string targetKey;
                string targetName;
                DateTime windowStart;
                DateTime windowEnd;
                if (!string.IsNullOrWhiteSpace(task.PhaseTempId))
                {
                    var target = phaseByTemp[task.PhaseTempId!.Trim()];
                    targetKey = "T:" + task.PhaseTempId!.Trim();
                    targetName = (target.Name ?? string.Empty).Trim();
                    windowStart = target.BaselineStart;
                    windowEnd = target.BaselineEnd;
                }
                else
                {
                    var target = existingById[task.PhaseId!.Value];
                    targetKey = "P:" + target.PhaseId;
                    targetName = target.Name;
                    windowStart = target.BaselineStart;
                    windowEnd = target.BaselineEnd;
                }
                if (task.BaselineStart < windowStart || task.BaselineEnd > windowEnd)
                    return new ApiResponse().SetBadRequest($"Task '{name}' must stay within the '{targetName}' phase baseline period.");
                if (!seenTaskNames.TryGetValue(targetKey, out var seen))
                {
                    seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    seenTaskNames[targetKey] = seen;
                }
                if (!seen.Add(name))
                    return new ApiResponse().SetBadRequest($"Duplicate task name '{name}' in phase '{targetName}'.");
                if (takenTaskNames.TryGetValue(task.PhaseId ?? 0, out var taken) && taken.Contains(name))
                    return new ApiResponse().SetConflict($"A task named '{name}' already exists in phase '{targetName}'.");
            }

            if (project.TotalProjectBudget > 0)
            {
                var existingTasks = await _uow.TaskItems.GetAllAsync(t => t.ProjectId == projectId &&
                    t.Status != DomainTaskStatus.CANCELLED && t.Status != DomainTaskStatus.REJECTED);
                if (existingTasks.Sum(t => t.PlannedBudget) + tasks.Sum(t => t.PlannedBudget) > project.TotalProjectBudget)
                    return new ApiResponse().SetConflict("Total planned task budgets cannot exceed the project budget.");
            }

            await _uow.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                var createdPhases = new List<ConfirmedAiPhaseResponse>();
                var phaseIdByTemp = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                var entityByTemp = new Dictionary<string, Phase>(StringComparer.OrdinalIgnoreCase);
                foreach (var phase in phases)
                {
                    var entity = new Phase
                    {
                        ProjectId = projectId,
                        Project = project,
                        WorkCategoryId = phase.WorkCategoryId,
                        WorkCategory = categoryById[phase.WorkCategoryId],
                        Name = phase.Name.Trim(),
                        Description = string.IsNullOrWhiteSpace(phase.Description) ? null : phase.Description.Trim(),
                        SequenceOrder = phase.SequenceOrder,
                        BaselineStart = phase.BaselineStart,
                        BaselineEnd = phase.BaselineEnd,
                        Status = PhaseStatus.PLANNED,
                        CreatedBy = pmId
                    };
                    await _uow.Phases.AddAsync(entity);
                    await _uow.SaveChangeAsync();
                    phaseIdByTemp[phase.TempId.Trim()] = entity.PhaseId;
                    entityByTemp[phase.TempId.Trim()] = entity;
                    createdPhases.Add(new ConfirmedAiPhaseResponse
                    {
                        TempId = phase.TempId.Trim(),
                        PhaseId = entity.PhaseId,
                        RowVersion = Convert.ToBase64String(entity.RowVersion)
                    });
                }

                var createdTasks = new List<ConfirmedAiTaskResponse>();
                foreach (var task in tasks)
                {
                    Phase targetPhase;
                    int targetPhaseId;
                    if (!string.IsNullOrWhiteSpace(task.PhaseTempId))
                    {
                        targetPhaseId = phaseIdByTemp[task.PhaseTempId!.Trim()];
                        targetPhase = entityByTemp[task.PhaseTempId!.Trim()];
                    }
                    else
                    {
                        targetPhaseId = task.PhaseId!.Value;
                        targetPhase = existingById[targetPhaseId];
                    }
                    var item = new TaskItem
                    {
                        ProjectId = projectId,
                        Project = project,
                        PhaseId = targetPhaseId,
                        Phase = targetPhase,
                        PhaseName = targetPhase.Name,
                        TaskName = task.TaskName.Trim(),
                        AssignedToUserID = pmId,
                        PlannedBudget = task.PlannedBudget,
                        ActualCost = 0,
                        ActualProgressPct = 0,
                        BaselineStart = task.BaselineStart,
                        BaselineEnd = task.BaselineEnd,
                        Status = DomainTaskStatus.PENDING
                    };
                    await _uow.TaskItems.AddAsync(item);
                    await _uow.SaveChangeAsync();
                    createdTasks.Add(new ConfirmedAiTaskResponse
                    {
                        TempId = task.TempId.Trim(),
                        TaskId = item.TaskId,
                        PhaseId = targetPhaseId,
                        RowVersion = Convert.ToBase64String(item.RowVersion)
                    });
                }

                await _uow.CommitTransactionAsync();
                return new ApiResponse().SetApiResponse(HttpStatusCode.Created, true, result: new ConfirmProjectAiPlanResponse
                {
                    Phases = createdPhases,
                    Tasks = createdTasks
                });
            }
            catch
            {
                await _uow.RollbackTransactionAsync();
                return new ApiResponse().SetApiResponse(HttpStatusCode.InternalServerError, false, "Unable to confirm the AI plan.");
            }
        }

        public async Task<ApiResponse> ImportProjectFromWordAiAsync(IFormFile? file)
        {
            const long maxWordSize = 10 * 1024 * 1024;
            if (file == null || file.Length == 0)
                return new ApiResponse().SetBadRequest("Upload a valid Word (.docx) file.");
            if (file.Length > maxWordSize || !string.Equals(Path.GetExtension(file.FileName), ".docx", StringComparison.OrdinalIgnoreCase))
                return new ApiResponse().SetBadRequest("The project import must be a .docx file no larger than 10 MB.");

            List<string> paragraphs;
            using (var stream = file.OpenReadStream())
            using (WordprocessingDocument wordDoc = WordprocessingDocument.Open(stream, false))
            {
                var body = wordDoc.MainDocumentPart?.Document?.Body;
                if (body == null)
                    return new ApiResponse().SetBadRequest("The Word document does not contain readable text.");
                paragraphs = body.Descendants<WordXml.Paragraph>()
                    .Select(p => p.InnerText.Trim())
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .ToList();
            }
            if (paragraphs.Count == 0)
                return new ApiResponse().SetBadRequest("The Word document does not contain readable text.");

            var aiResult = await _googleAIClient.GenerateTextAsync(
                BuildExtractionInstruction(), BuildExtractionPrompt(paragraphs));
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

            AiExtractedProjectPlan? extracted = null;
            var json = ExtractJsonObject(aiResult.Text);
            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    extracted = JsonSerializer.Deserialize<AiExtractedProjectPlan>(json, JsonOptions);
                }
                catch (JsonException ex)
                {
                    return new ApiResponse().SetBadRequest(
                        new { errorCode = "AI_JSON_INVALID", detail = ex.Message },
                        "AI returned invalid import JSON. Please try again.");
                }
            }
            if (extracted == null)
            {
                return new ApiResponse().SetBadRequest(
                    new { errorCode = "AI_JSON_INVALID", detail = "No JSON object was found in the AI response." },
                    "AI returned invalid import JSON. Please try again.");
            }

            var (error, preview) = MapExtractedPlan(extracted);
            if (error != null)
                return error;

            return new ApiResponse().SetOk(preview);
        }

        private static string BuildExtractionInstruction() =>
            "You extract construction project data from a Word document. Return only valid JSON matching the required schema. " +
            "Do not include Markdown, comments, explanations, or code fences. Use null or omit fields you cannot determine. " +
            "Keep every phase and task name short and specific.";

        private static string BuildExtractionPrompt(List<string> paragraphs)
        {
            const int maxChars = 20000;
            var text = string.Join("\n", paragraphs);
            var truncated = text.Length > maxChars;
            if (truncated)
                text = text[..maxChars];

            var builder = new StringBuilder();
            builder.AppendLine("Extract the construction project, its phases, and its tasks from the document text below.");
            builder.AppendLine("Dates use YYYY-MM-DD. Budgets are plain numbers without currency symbols or separators.");
            builder.AppendLine("Each task's phaseRef is either the exact phase name or the 1-based phase number.");
            if (truncated)
                builder.AppendLine("Note: the document text was truncated; extract what is visible.");
            builder.AppendLine();
            builder.AppendLine("Return JSON with this exact top-level shape:");
            builder.AppendLine("""
{
  "projectName": "string (required)",
  "address": "string or null",
  "totalBudget": 0,
  "currency": "string or null",
  "startDate": "YYYY-MM-DD or null",
  "baselineStart": "YYYY-MM-DD or null",
  "baselineEnd": "YYYY-MM-DD or null",
  "phases": [
    { "name": "string (required)", "description": "string or null", "sequenceOrder": 0, "baselineStart": "YYYY-MM-DD", "baselineEnd": "YYYY-MM-DD" }
  ],
  "tasks": [
    { "phaseRef": "string (required)", "taskName": "string (required)", "plannedBudget": 0, "baselineStart": "YYYY-MM-DD", "baselineEnd": "YYYY-MM-DD" }
  ]
}
""");
            builder.AppendLine("Document text:");
            builder.AppendLine(text);
            return builder.ToString();
        }

        private static (ApiResponse? Error, AiImportPreviewResponse? Preview) MapExtractedPlan(AiExtractedProjectPlan extracted)
        {
            const int maxPhases = 100;
            const int maxTasks = 500;

            var projectName = (extracted.ProjectName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(projectName) || projectName.Length > 200)
                return (new ApiResponse().SetBadRequest(
                    new { errorCode = "AI_IMPORT_INVALID", detail = "projectName is required and must not exceed 200 characters." },
                    "AI could not determine the project name from the document."), null);
            var address = string.IsNullOrWhiteSpace(extracted.Address) ? null : extracted.Address.Trim();
            if (address?.Length > 500)
                return (BadImport("Address must not exceed 500 characters."), null);
            if (extracted.TotalBudget < 0)
                return (BadImport("Total budget cannot be negative."), null);

            var startDate = extracted.StartDate == default ? DateTime.UtcNow.Date : extracted.StartDate.Date;
            var baselineStart = extracted.BaselineStart == default ? startDate : extracted.BaselineStart.Date;
            var baselineEnd = extracted.BaselineEnd == default ? baselineStart.AddMonths(6) : extracted.BaselineEnd.Date;
            if (baselineEnd < baselineStart || startDate > baselineEnd)
                return (BadImport("Project baseline dates are invalid."), null);

            var phases = extracted.Phases ?? new List<AiExtractedPhase>();
            var tasks = extracted.Tasks ?? new List<AiExtractedTask>();
            if (phases.Count > maxPhases || tasks.Count > maxTasks)
                return (BadImport($"The document contains too many items (at most {maxPhases} phases and {maxTasks} tasks)."), null);

            var previewPhases = new List<AiPhaseProposalResponse>();
            for (var index = 0; index < phases.Count; index++)
            {
                var phase = phases[index];
                var name = (phase.Name ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
                    return (BadImport($"Phase #{index + 1} must have a name of at most 200 characters."), null);
                if (phase.Description?.Trim().Length > 2000)
                    return (BadImport($"Phase '{name}' description must not exceed 2000 characters."), null);
                if (phase.SequenceOrder < 0)
                    return (BadImport($"Phase '{name}' sequence order cannot be negative."), null);
                if (phase.BaselineEnd < phase.BaselineStart)
                    return (BadImport($"Phase '{name}' baseline dates are invalid."), null);
                if (phase.BaselineStart < baselineStart || phase.BaselineEnd > baselineEnd)
                    return (BadImport($"Phase '{name}' must stay inside the project baseline period."), null);
                if (previewPhases.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
                    return (BadImport($"Duplicate phase name '{name}'."), null);
                var aiKey = $"P{index + 1:00}";
                previewPhases.Add(new AiPhaseProposalResponse
                {
                    TempId = $"PH-{aiKey}",
                    AiKey = aiKey,
                    Name = name,
                    Description = string.IsNullOrWhiteSpace(phase.Description) ? null : phase.Description.Trim(),
                    SequenceOrder = phase.SequenceOrder,
                    BaselineStart = phase.BaselineStart,
                    BaselineEnd = phase.BaselineEnd
                });
            }

            var previewTasks = new List<AiTaskProposalResponse>();
            for (var index = 0; index < tasks.Count; index++)
            {
                var task = tasks[index];
                var taskName = (task.TaskName ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(taskName) || taskName.Length > 200)
                    return (BadImport($"Task #{index + 1} must have a name of at most 200 characters."), null);
                if (task.PlannedBudget < 0)
                    return (BadImport($"Task '{taskName}' planned budget cannot be negative."), null);
                var phase = ResolvePhaseRef(previewPhases, task.PhaseRef);
                if (phase == null)
                    return (BadImport($"Task '{taskName}' references unknown phase '{(task.PhaseRef ?? string.Empty).Trim()}'."), null);
                if (task.BaselineEnd < task.BaselineStart)
                    return (BadImport($"Task '{taskName}' baseline dates are invalid."), null);
                if (task.BaselineStart < phase.BaselineStart || task.BaselineEnd > phase.BaselineEnd)
                    return (BadImport($"Task '{taskName}' must stay inside the '{phase.Name}' phase baseline period."), null);
                if (previewTasks.Any(t => string.Equals(t.PhaseTempId, phase.TempId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(t.TaskName, taskName, StringComparison.OrdinalIgnoreCase)))
                    return (BadImport($"Duplicate task name '{taskName}' in phase '{phase.Name}'."), null);
                previewTasks.Add(new AiTaskProposalResponse
                {
                    TempId = $"TSK-T{index + 1:000}",
                    PhaseTempId = phase.TempId,
                    TaskName = taskName,
                    BaselineStart = task.BaselineStart,
                    BaselineEnd = task.BaselineEnd,
                    PlannedBudget = task.PlannedBudget
                });
            }

            return (null, new AiImportPreviewResponse
            {
                Project = new AiImportProjectResponse
                {
                    ProjectName = projectName,
                    Address = address,
                    TotalBudget = extracted.TotalBudget,
                    Currency = string.IsNullOrWhiteSpace(extracted.Currency) ? "VND" : extracted.Currency.Trim(),
                    StartDate = startDate,
                    BaselineStart = baselineStart,
                    BaselineEnd = baselineEnd
                },
                Plan = new ProjectAiPlanPreviewResponse
                {
                    Phases = previewPhases,
                    Tasks = previewTasks
                }
            });
        }

        private static AiPhaseProposalResponse? ResolvePhaseRef(List<AiPhaseProposalResponse> phases, string? phaseRef)
        {
            var key = (phaseRef ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(key))
                return null;
            if (int.TryParse(key, out var number) && number >= 1 && number <= phases.Count)
                return phases[number - 1];
            return phases.FirstOrDefault(p => string.Equals(p.Name, key, StringComparison.OrdinalIgnoreCase));
        }

        private static ApiResponse BadImport(string detail) =>
            new ApiResponse().SetBadRequest(new { errorCode = "AI_IMPORT_INVALID", detail }, detail);

        private static AiPhaseProposalResponse MapPhaseProposal(
            Project project, ConstructionPhaseRowResponse row, int index, HashSet<string> usedTemps)
        {
            var aiKey = string.IsNullOrWhiteSpace(row.PhaseId) ? $"P{index + 1:00}" : row.PhaseId.Trim();
            var (start, end) = MapWeeksToDates(project, row.StartWeek, row.EndWeek);
            return new AiPhaseProposalResponse
            {
                TempId = UniqueTempId($"PH-{aiKey}", usedTemps),
                AiKey = aiKey,
                Name = string.IsNullOrWhiteSpace(row.PhaseName) ? $"Phase {index + 1}" : row.PhaseName.Trim(),
                Description = string.IsNullOrWhiteSpace(row.Description) ? null : row.Description.Trim(),
                SequenceOrder = index,
                BaselineStart = start,
                BaselineEnd = end
            };
        }

        private static AiTaskProposalResponse MapTaskProposal(
            Project project, ConstructionTaskRowResponse row, int index, string tempId,
            string? phaseTempId, int? phaseId, DateTime windowStart, DateTime windowEnd)
        {
            var (start, end) = MapWeeksToDates(project, row.StartWeek, row.EndWeek);
            if (start < windowStart) start = windowStart;
            if (start > windowEnd) start = windowEnd;
            if (end < windowStart) end = windowStart;
            if (end > windowEnd) end = windowEnd;
            if (end < start) end = start;
            return new AiTaskProposalResponse
            {
                TempId = tempId,
                PhaseTempId = phaseTempId,
                PhaseId = phaseId,
                TaskName = string.IsNullOrWhiteSpace(row.TaskName) ? $"Task {index + 1}" : row.TaskName.Trim(),
                BaselineStart = start,
                BaselineEnd = end,
                PlannedBudget = row.EstimatedCost < 0 ? 0 : row.EstimatedCost
            };
        }

        private static (DateTime Start, DateTime End) MapWeeksToDates(Project project, int startWeek, int endWeek)
        {
            var baselineStart = project.BaselineStart.Date;
            var baselineEnd = project.BaselineEnd.Date;
            var start = baselineStart.AddDays(Math.Max(startWeek - 1, 0) * 7);
            var end = baselineStart.AddDays(Math.Max(Math.Max(endWeek, startWeek), 1) * 7).AddDays(-1);
            if (start < baselineStart) start = baselineStart;
            if (start > baselineEnd) start = baselineEnd;
            if (end < baselineStart) end = baselineStart;
            if (end > baselineEnd) end = baselineEnd;
            if (end < start) end = start;
            return (start, end);
        }

        private static string? FindMatchingAiPhaseKey(ConstructionPlanJsonResponse plan, string phaseName)
        {
            foreach (var row in plan.ExcelSheets.Phases)
            {
                if (string.Equals((row.PhaseName ?? string.Empty).Trim(), (phaseName ?? string.Empty).Trim(),
                    StringComparison.OrdinalIgnoreCase))
                {
                    var key = (row.PhaseId ?? string.Empty).Trim();
                    return string.IsNullOrWhiteSpace(key) ? null : key;
                }
            }
            return null;
        }

        private static string UniqueTempId(string candidate, HashSet<string> used)
        {
            var temp = candidate;
            var suffix = 2;
            while (!used.Add(temp))
                temp = $"{candidate}-{suffix++}";
            return temp;
        }

        private static List<ConstructionPlannerQuestionResponse> BuildQuestions() => new()
        {
            new ConstructionPlannerQuestionResponse
            {
                Order = 1,
                Field = "projectOverview",
                Label = "What type of construction project do you want to build, and what is the target size or scope?",
                Required = true,
                Placeholder = "Example: 3-floor residential house, 250 m2 total floor area"
            },
            new ConstructionPlannerQuestionResponse
            {
                Order = 2,
                Field = "locationAndSite",
                Label = "Where is the project located, and are there any important site conditions or constraints?",
                Required = true,
                Placeholder = "Example: District 7, Ho Chi Minh City; narrow alley access; flat site"
            },
            new ConstructionPlannerQuestionResponse
            {
                Order = 3,
                Field = "timeline",
                Label = "What is the target start date and desired completion date or duration?",
                Required = true,
                Placeholder = "Example: Start 2026-10-01, finish within 8 months"
            },
            new ConstructionPlannerQuestionResponse
            {
                Order = 4,
                Field = "budgetAndQuality",
                Label = "What is the estimated budget, currency, and expected quality level?",
                Required = true,
                Placeholder = "Example: 3.5 billion VND, mid-high quality"
            },
            new ConstructionPlannerQuestionResponse
            {
                Order = 5,
                Field = "specialRequirements",
                Label = "What special requirements should the plan include, such as permits, sustainability, safety, suppliers, or risk concerns?",
                Required = true,
                Placeholder = "Example: Include permits, safety plan, sustainable materials, and supplier planning"
            }
        };

        private static ApiResponse? ValidateRequest(GenerateConstructionPlanRequest? request)
            => ValidateAnswers(request?.Answers);

        private static ApiResponse? ValidateAnswers(ConstructionPlanAnswersRequest? answers)
        {
            if (answers == null)
                return PlannerBadRequest("All five planning answers are required.", "PLANNER_ANSWERS_INCOMPLETE");

            if (IsMissing(answers.ProjectOverview) ||
                IsMissing(answers.LocationAndSite) ||
                IsMissing(answers.Timeline) ||
                IsMissing(answers.BudgetAndQuality))
            {
                return PlannerBadRequest("Project overview, location/site, timeline, and budget/quality answers are required.", "PLANNER_ANSWERS_INCOMPLETE");
            }

            if (Length(answers.ProjectOverview) < 20)
                return PlannerBadRequest("Project overview must be at least 20 characters.", "PLANNER_PROJECT_OVERVIEW_TOO_SHORT");
            if (Length(answers.LocationAndSite) < 10)
                return PlannerBadRequest("Location and site answer must be at least 10 characters.", "PLANNER_LOCATION_TOO_SHORT");
            if (Length(answers.Timeline) < 10)
                return PlannerBadRequest("Timeline answer must be at least 10 characters.", "PLANNER_TIMELINE_TOO_SHORT");
            if (Length(answers.BudgetAndQuality) < 10)
                return PlannerBadRequest("Budget and quality answer must be at least 10 characters.", "PLANNER_BUDGET_TOO_SHORT");

            if (Length(answers.ProjectOverview) > MaxLongAnswerLength ||
                Length(answers.LocationAndSite) > MaxLongAnswerLength ||
                Length(answers.SpecialRequirements) > MaxLongAnswerLength ||
                Length(answers.Timeline) > MaxShortAnswerLength ||
                Length(answers.BudgetAndQuality) > MaxShortAnswerLength)
            {
                return PlannerBadRequest("One or more planning answers are too long.", "PLANNER_ANSWER_TOO_LONG");
            }

            return null;
        }

        private static ApiResponse PlannerBadRequest(string message, string errorCode) =>
            new ApiResponse().SetBadRequest(new { errorCode }, message);

        private static bool IsMissing(string? value) => string.IsNullOrWhiteSpace(value);
        private static int Length(string? value) => value?.Trim().Length ?? 0;

        private static ConstructionPlanAnswersRequest NormalizeAnswers(ConstructionPlanAnswersRequest answers) => new()
        {
            ProjectOverview = answers.ProjectOverview!.Trim(),
            LocationAndSite = answers.LocationAndSite!.Trim(),
            Timeline = answers.Timeline!.Trim(),
            BudgetAndQuality = answers.BudgetAndQuality!.Trim(),
            SpecialRequirements = string.IsNullOrWhiteSpace(answers.SpecialRequirements)
                ? "None"
                : answers.SpecialRequirements.Trim()
        };

        private static string BuildSystemInstruction() =>
            "You are BuildSense AI Construction Planner. Generate a practical construction plan from five user answers. " +
            "Return only valid JSON matching the required schema. Do not include Markdown, comments, explanations, or code fences. " +
            "Use conservative assumptions when details are missing. Mark uncertain values in notes or assumptions. " +
            "Costs and schedules are planning estimates only and must be validated by qualified local professionals before execution.";

        private static string BuildPrompt(ConstructionPlanAnswersRequest answers, Project? project, string? completionContext = null)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Generate an Excel-ready construction plan JSON.");
            builder.AppendLine();

            if (project != null)
            {
                builder.AppendLine("Existing BuildSense project context:");
                builder.AppendLine($"Project ID: {project.ProjectId}");
                builder.AppendLine($"Project name: {project.ProjectName}");
                builder.AppendLine($"Address: {project.Address ?? "Not specified"}");
                builder.AppendLine($"Budget: {project.TotalProjectBudget} {project.Currency}");
                builder.AppendLine($"Baseline start: {project.BaselineStart:yyyy-MM-dd}");
                builder.AppendLine($"Baseline end: {project.BaselineEnd:yyyy-MM-dd}");
                builder.AppendLine();
            }

            if (!string.IsNullOrWhiteSpace(completionContext))
            {
                builder.AppendLine(completionContext);
                builder.AppendLine();
            }

            AppendAnswerLines(builder, answers);
            AppendJsonShape(builder);
            builder.AppendLine("Populate every sheet array with useful construction planning rows. Use numeric values for quantities, costs, weeks, and durations.");
            return builder.ToString();
        }

        private static void AppendJsonShape(StringBuilder builder)
        {
            builder.AppendLine("Return JSON with this exact top-level shape:");
            builder.AppendLine("""
{
  "planId": "string",
  "version": "1.0",
  "generatedAt": "ISO-8601 UTC datetime",
  "projectSummary": {
    "projectName": "string",
    "projectType": "string",
    "location": "string",
    "scope": "string",
    "assumptions": ["string"],
    "currency": "string",
    "estimatedBudget": 0,
    "targetStartDate": "YYYY-MM-DD or null",
    "targetEndDate": "YYYY-MM-DD or null",
    "estimatedDurationDays": 0
  },
  "excelSheets": {
    "overview": [],
    "phases": [],
    "tasks": [],
    "materials": [],
    "labor": [],
    "equipment": [],
    "costPlan": [],
    "procurementPlan": [],
    "riskRegister": [],
    "permitChecklist": [],
    "safetyPlan": [],
    "milestones": []
  }
}
""");
        }

        private static void AppendAnswerLines(StringBuilder builder, ConstructionPlanAnswersRequest answers)
        {
            builder.AppendLine("Question 1 - Project overview:");
            builder.AppendLine(answers.ProjectOverview);
            builder.AppendLine();
            builder.AppendLine("Question 2 - Location and site:");
            builder.AppendLine(answers.LocationAndSite);
            builder.AppendLine();
            builder.AppendLine("Question 3 - Timeline:");
            builder.AppendLine(answers.Timeline);
            builder.AppendLine();
            builder.AppendLine("Question 4 - Budget and quality:");
            builder.AppendLine(answers.BudgetAndQuality);
            builder.AppendLine();
            builder.AppendLine("Question 5 - Special requirements:");
            builder.AppendLine(answers.SpecialRequirements);
            builder.AppendLine();
        }

        private static ApiResponse? ValidateBrief(AiProjectBriefRequest? brief)
        {
            if (brief == null)
                return PlannerBadRequest("Project brief is required.", "PLANNER_BRIEF_REQUIRED");
            if (string.IsNullOrWhiteSpace(brief.ProjectType) || brief.ProjectType.Trim().Length > 200)
                return PlannerBadRequest("Project type is required and must not exceed 200 characters.", "PLANNER_BRIEF_INVALID");
            if (brief.FloorAreaM2 <= 0)
                return PlannerBadRequest("Floor area must be greater than zero.", "PLANNER_BRIEF_INVALID");
            if (brief.NumberOfFloors < 1)
                return PlannerBadRequest("Number of floors must be at least one.", "PLANNER_BRIEF_INVALID");
            if (brief.StartDate == default)
                return PlannerBadRequest("Start date is required.", "PLANNER_BRIEF_INVALID");
            if (brief.EndDate == default)
                return PlannerBadRequest("End date is required.", "PLANNER_BRIEF_INVALID");
            if (brief.EndDate < brief.StartDate)
                return PlannerBadRequest("End date cannot be before the start date.", "PLANNER_BRIEF_INVALID");
            if (brief.Budget.HasValue && brief.Budget.Value < 0)
                return PlannerBadRequest("Budget cannot be negative.", "PLANNER_BRIEF_INVALID");
            if (Length(brief.SpecialRequirements) > MaxLongAnswerLength)
                return PlannerBadRequest("Special requirements are too long.", "PLANNER_BRIEF_TOO_LONG");
            return null;
        }

        private static string BuildPromptFromBrief(AiProjectBriefRequest brief, Project? project, string? completionContext = null)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Generate an Excel-ready construction plan JSON.");
            builder.AppendLine();
            builder.AppendLine("Structured project brief (authoritative; prefer these exact values over any conflicting text):");
            builder.AppendLine($"Project type: {brief.ProjectType.Trim()}");
            builder.AppendLine($"Floor area: {brief.FloorAreaM2.ToString(System.Globalization.CultureInfo.InvariantCulture)} m2");
            builder.AppendLine($"Number of floors: {brief.NumberOfFloors}");
            builder.AppendLine($"Target start date: {brief.StartDate:yyyy-MM-dd}");
            builder.AppendLine($"Target end date: {brief.EndDate:yyyy-MM-dd}");
            builder.AppendLine($"Budget: {(brief.Budget.HasValue ? brief.Budget.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "Not specified")}");
            builder.AppendLine($"Special requirements: {(string.IsNullOrWhiteSpace(brief.SpecialRequirements) ? "None" : brief.SpecialRequirements.Trim())}");
            builder.AppendLine();
            builder.AppendLine("Anchor all schedule weeks to the target start date above and fit the whole plan between the start and end dates.");
            builder.AppendLine();

            if (!string.IsNullOrWhiteSpace(completionContext))
            {
                builder.AppendLine(completionContext);
                builder.AppendLine();
            }

            if (project != null)
            {
                builder.AppendLine("Existing BuildSense project context:");
                builder.AppendLine($"Project ID: {project.ProjectId}");
                builder.AppendLine($"Project name: {project.ProjectName}");
                builder.AppendLine($"Address: {project.Address ?? "Not specified"}");
                builder.AppendLine($"Budget: {project.TotalProjectBudget} {project.Currency}");
                builder.AppendLine($"Baseline start: {project.BaselineStart:yyyy-MM-dd}");
                builder.AppendLine($"Baseline end: {project.BaselineEnd:yyyy-MM-dd}");
                builder.AppendLine();
            }

            AppendAnswerLines(builder, ToAnswers(brief));
            AppendJsonShape(builder);
            builder.AppendLine("Populate every sheet array with useful construction planning rows. Use numeric values for quantities, costs, weeks, and durations.");
            return builder.ToString();
        }

        private static ConstructionPlanAnswersRequest ToAnswers(AiProjectBriefRequest brief) => new()
        {
            ProjectOverview = $"{brief.ProjectType.Trim()} construction project, " +
                $"{brief.FloorAreaM2.ToString(System.Globalization.CultureInfo.InvariantCulture)} m2 total floor area, " +
                $"{brief.NumberOfFloors} floor(s)",
            LocationAndSite = "Not specified",
            Timeline = $"Start {brief.StartDate:yyyy-MM-dd} and finish by {brief.EndDate:yyyy-MM-dd}",
            BudgetAndQuality = brief.Budget.HasValue
                ? $"{brief.Budget.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)} planned budget"
                : "Budget to be confirmed with standard quality finishes",
            SpecialRequirements = string.IsNullOrWhiteSpace(brief.SpecialRequirements)
                ? "None"
                : brief.SpecialRequirements.Trim()
        };

        private static bool TryParsePlan(string? aiText, out ConstructionPlanJsonResponse? plan, out string? error)
        {
            plan = null;
            error = null;

            if (string.IsNullOrWhiteSpace(aiText))
            {
                error = "AI response was empty.";
                return false;
            }

            var json = ExtractJsonObject(aiText);
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "No JSON object was found in the AI response.";
                return false;
            }

            try
            {
                plan = JsonSerializer.Deserialize<ConstructionPlanJsonResponse>(json, JsonOptions);
                if (plan == null)
                {
                    error = "JSON deserialized to null.";
                    return false;
                }

                return true;
            }
            catch (JsonException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static string? ExtractJsonObject(string text)
        {
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

        private static void NormalizePlan(ConstructionPlanJsonResponse plan)
        {
            if (string.IsNullOrWhiteSpace(plan.PlanId))
                plan.PlanId = Guid.NewGuid().ToString("N");
            if (string.IsNullOrWhiteSpace(plan.Version))
                plan.Version = PlannerVersion;
            if (plan.GeneratedAt == default)
                plan.GeneratedAt = DateTime.UtcNow;

            plan.ProjectSummary ??= new ConstructionProjectSummaryResponse();
            plan.ProjectSummary.Assumptions ??= new List<string>();
            plan.ExcelSheets ??= new ConstructionPlanExcelSheetsResponse();
            plan.ExcelSheets.Overview ??= new List<ConstructionOverviewRowResponse>();
            plan.ExcelSheets.Phases ??= new List<ConstructionPhaseRowResponse>();
            plan.ExcelSheets.Tasks ??= new List<ConstructionTaskRowResponse>();
            plan.ExcelSheets.Materials ??= new List<ConstructionMaterialRowResponse>();
            plan.ExcelSheets.Labor ??= new List<ConstructionLaborRowResponse>();
            plan.ExcelSheets.Equipment ??= new List<ConstructionEquipmentRowResponse>();
            plan.ExcelSheets.CostPlan ??= new List<ConstructionCostPlanRowResponse>();
            plan.ExcelSheets.ProcurementPlan ??= new List<ConstructionProcurementPlanRowResponse>();
            plan.ExcelSheets.RiskRegister ??= new List<ConstructionRiskRegisterRowResponse>();
            plan.ExcelSheets.PermitChecklist ??= new List<ConstructionPermitChecklistRowResponse>();
            plan.ExcelSheets.SafetyPlan ??= new List<ConstructionSafetyPlanRowResponse>();
            plan.ExcelSheets.Milestones ??= new List<ConstructionMilestoneRowResponse>();
        }

        private static string? ValidatePlanContract(ConstructionPlanJsonResponse plan)
        {
            if (plan.ProjectSummary == null)
                return "projectSummary is required.";
            if (plan.ExcelSheets == null)
                return "excelSheets is required.";
            if (string.IsNullOrWhiteSpace(plan.ProjectSummary.ProjectName))
                return "projectSummary.projectName is required.";
            if (plan.ExcelSheets.Overview.Count == 0)
                return "excelSheets.overview must contain at least one row.";
            if (plan.ExcelSheets.Phases.Count == 0)
                return "excelSheets.phases must contain at least one row.";
            if (plan.ExcelSheets.Tasks.Count == 0)
                return "excelSheets.tasks must contain at least one row.";
            return null;
        }

        private static string BuildDownloadFileName(string? requestedFileName, ConstructionPlanJsonResponse plan)
        {
            var fallback = string.IsNullOrWhiteSpace(plan.ProjectSummary.ProjectName)
                ? "construction-plan"
                : plan.ProjectSummary.ProjectName;
            return ExcelSheetWriter.BuildDownloadFileName(requestedFileName, fallback);
        }

    }
}
