using ClosedXML.Excel;
using cpms_Application.Interfaces;
using cpms_Application.Request.AiConstructionPlanner;
using cpms_Application.Response;
using cpms_Application.Response.AiConstructionPlanner;
using cpms_Domain.Models;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;

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
        private readonly IGoogleAIClient _googleAIClient;

        public AiConstructionPlannerService(
            IUnitOfWork uow,
            IClaimService claimService,
            IGoogleAIClient googleAIClient)
        {
            _uow = uow;
            _claimService = claimService;
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

                if (!await CanReadProjectAsync(project))
                    return new ApiResponse().SetApiResponse(HttpStatusCode.Forbidden, false, "You do not have access to this project.");
            }

            var answers = NormalizeAnswers(request.Answers!);
            var systemInstruction = BuildSystemInstruction();
            var prompt = BuildPrompt(answers, project);
            var aiResult = await _googleAIClient.GenerateTextAsync(systemInstruction, prompt);

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

            if (!TryParsePlan(aiResult.Text, out var plan, out var parseError))
            {
                return new ApiResponse().SetBadRequest(
                    new { errorCode = "AI_JSON_INVALID", detail = parseError },
                    "AI returned invalid planner JSON. Please try again.");
            }

            NormalizePlan(plan!);

            var contractError = ValidatePlanContract(plan!);
            if (contractError != null)
            {
                return new ApiResponse().SetBadRequest(
                    new { errorCode = "AI_JSON_CONTRACT_INVALID", detail = contractError },
                    "AI returned planner JSON that does not match the expected Excel plan contract.");
            }

            return new ApiResponse().SetOk(plan);
        }

        public Task<ApiResponse> GenerateExcelAsync(GenerateConstructionPlanExcelRequest request)
        {
            if (request?.Plan == null)
            {
                return Task.FromResult(PlannerBadRequest(
                    "A generated construction plan JSON payload is required.",
                    "PLANNER_JSON_REQUIRED"));
            }

            NormalizePlan(request.Plan);
            var contractError = ValidatePlanContract(request.Plan);
            if (contractError != null)
            {
                return Task.FromResult(new ApiResponse().SetBadRequest(
                    new { errorCode = "PLANNER_JSON_CONTRACT_INVALID", detail = contractError },
                    "Planner JSON does not match the expected Excel plan contract."));
            }

            using var workbook = new XLWorkbook();
            AddSheet(workbook, "Overview", request.Plan.ExcelSheets.Overview);
            AddSheet(workbook, "Phases", request.Plan.ExcelSheets.Phases);
            AddSheet(workbook, "Tasks", request.Plan.ExcelSheets.Tasks);
            AddSheet(workbook, "Materials", request.Plan.ExcelSheets.Materials);
            AddSheet(workbook, "Labor", request.Plan.ExcelSheets.Labor);
            AddSheet(workbook, "Equipment", request.Plan.ExcelSheets.Equipment);
            AddSheet(workbook, "Cost Plan", request.Plan.ExcelSheets.CostPlan);
            AddSheet(workbook, "Procurement Plan", request.Plan.ExcelSheets.ProcurementPlan);
            AddSheet(workbook, "Risk Register", request.Plan.ExcelSheets.RiskRegister);
            AddSheet(workbook, "Permit Checklist", request.Plan.ExcelSheets.PermitChecklist);
            AddSheet(workbook, "Safety Plan", request.Plan.ExcelSheets.SafetyPlan);
            AddSheet(workbook, "Milestones", request.Plan.ExcelSheets.Milestones);

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);

            var file = new ConstructionPlanExcelFileResponse
            {
                Content = stream.ToArray(),
                FileName = BuildDownloadFileName(request.FileName, request.Plan)
            };

            return Task.FromResult(new ApiResponse().SetOk(file));
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
        {
            if (request?.Answers == null)
                return PlannerBadRequest("All five planning answers are required.", "PLANNER_ANSWERS_INCOMPLETE");

            var answers = request.Answers;
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

        private static string BuildPrompt(ConstructionPlanAnswersRequest answers, Project? project)
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
            builder.AppendLine("Populate every sheet array with useful construction planning rows. Use numeric values for quantities, costs, weeks, and durations.");
            return builder.ToString();
        }

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

        private static void AddSheet<T>(XLWorkbook workbook, string sheetName, IReadOnlyCollection<T> rows)
        {
            var worksheet = workbook.Worksheets.Add(sheetName);
            var properties = typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public);

            for (var column = 0; column < properties.Length; column++)
            {
                var cell = worksheet.Cell(1, column + 1);
                cell.Value = ToHeader(properties[column].Name);
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#E8F1FF");
                cell.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            }

            var rowNumber = 2;
            foreach (var row in rows)
            {
                for (var column = 0; column < properties.Length; column++)
                {
                    var value = properties[column].GetValue(row);
                    worksheet.Cell(rowNumber, column + 1).Value = ToExcelValue(value);
                }

                rowNumber++;
            }

            if (properties.Length > 0)
            {
                var range = worksheet.Range(1, 1, Math.Max(rowNumber - 1, 1), properties.Length);
                range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
                range.Style.Alignment.WrapText = true;
                worksheet.SheetView.FreezeRows(1);
                worksheet.Columns(1, properties.Length).AdjustToContents();
            }
        }

        private static XLCellValue ToExcelValue(object? value)
        {
            if (value == null)
                return string.Empty;
            if (value is string stringValue)
                return stringValue;
            if (value is IEnumerable<string> stringValues)
                return string.Join("; ", stringValues.Where(v => !string.IsNullOrWhiteSpace(v)));
            if (value is int intValue)
                return intValue;
            if (value is decimal decimalValue)
                return decimalValue;
            if (value is double doubleValue)
                return doubleValue;
            if (value is bool boolValue)
                return boolValue;
            if (value is DateTime dateTimeValue)
                return dateTimeValue;

            return value.ToString() ?? string.Empty;
        }

        private static string ToHeader(string propertyName)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
                return string.Empty;

            var builder = new StringBuilder();
            builder.Append(propertyName[0]);
            for (var i = 1; i < propertyName.Length; i++)
            {
                if (char.IsUpper(propertyName[i]) && !char.IsWhiteSpace(propertyName[i - 1]))
                    builder.Append(' ');
                builder.Append(propertyName[i]);
            }

            return builder.ToString();
        }

        private static string BuildDownloadFileName(string? requestedFileName, ConstructionPlanJsonResponse plan)
        {
            var baseName = string.IsNullOrWhiteSpace(requestedFileName)
                ? plan.ProjectSummary.ProjectName
                : requestedFileName;
            if (string.IsNullOrWhiteSpace(baseName))
                baseName = "construction-plan";

            foreach (var invalid in Path.GetInvalidFileNameChars())
                baseName = baseName.Replace(invalid, '-');

            baseName = baseName.Trim();
            if (baseName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                baseName = baseName[..^5];
            if (string.IsNullOrWhiteSpace(baseName))
                baseName = "construction-plan";

            return $"{baseName}-{DateTime.UtcNow:yyyyMMdd-HHmm}.xlsx";
        }

        private async Task<bool> CanReadProjectAsync(Project project)
        {
            var currentUser = _claimService.GetUserClaim();
            if (IsRole(currentUser, Role.ADMIN)) return true;
            if (IsRole(currentUser, Role.PM)) return project.PMUserID == currentUser.Id;
            if (!IsRole(currentUser, Role.WAREHOUSE_MANAGER)) return false;

            var linkedRequest = await _uow.MaterialRequests.GetAsync(r =>
                r.ProjectId == project.ProjectId && r.WarehouseId.HasValue && r.Warehouse!.ManagerId == currentUser.Id);
            if (linkedRequest != null) return true;

            var linkedOrder = await _uow.PurchaseOrders.GetAsync(o =>
                o.ProjectId == project.ProjectId && o.Warehouse.ManagerId == currentUser.Id);
            return linkedOrder != null;
        }

        private static bool IsRole(ClaimDTO claim, Role role) =>
            string.Equals(claim.Role, role.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
