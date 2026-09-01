using ClosedXML.Excel;
using cpms_Application.Interfaces;
using cpms_Application.Request.AiConstructionPlanner;
using cpms_Application.Response.AiConstructionPlanner;
using cpms_Application.Services;
using cpms_Domain.Models;
using System.Net;
using System.Text.Json;

namespace cpms_Tests;

public class AiConstructionPlannerServiceTests
{
    [Fact]
    public async Task GetQuestionsReturnsExactlyFivePlannerQuestions()
    {
        var service = CreateService(new TestUnitOfWork());

        var response = await service.GetQuestionsAsync();

        Assert.True(response.IsSuccess);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = Assert.IsType<ConstructionPlannerQuestionsResponse>(response.Result);
        Assert.Equal("1.0", result.Version);
        Assert.Equal(5, result.Questions.Count);
        Assert.Equal(new[]
        {
            "projectOverview",
            "locationAndSite",
            "timeline",
            "budgetAndQuality",
            "specialRequirements"
        }, result.Questions.Select(q => q.Field).ToArray());
        Assert.All(result.Questions, q => Assert.True(q.Required));
    }

    [Fact]
    public async Task GeneratePlanJsonRejectsIncompleteAnswers()
    {
        var service = CreateService(new TestUnitOfWork());

        var response = await service.GeneratePlanJsonAsync(new GenerateConstructionPlanRequest
        {
            Answers = new ConstructionPlanAnswersRequest
            {
                ProjectOverview = "Small house"
            }
        });

        Assert.False(response.IsSuccess);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("required", response.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GeneratePlanJsonUsesGeminiAndReturnsTypedExcelPlan()
    {
        var google = new FakeGoogleAIClient { NextResult = GoogleAITextResult.Success(ValidPlanJson) };
        var service = CreateService(new TestUnitOfWork(), google);

        var response = await service.GeneratePlanJsonAsync(ValidRequest());

        Assert.True(response.IsSuccess);
        Assert.Equal(1, google.CallCount);
        Assert.Contains("Question 1 - Project overview", google.LastInput);

        var result = Assert.IsType<ConstructionPlanJsonResponse>(response.Result);
        Assert.Equal("plan-001", result.PlanId);
        Assert.Equal("BuildSense Test House", result.ProjectSummary.ProjectName);
        Assert.Single(result.ExcelSheets.Overview);
        Assert.Single(result.ExcelSheets.Phases);
        Assert.Single(result.ExcelSheets.Tasks);
        Assert.Single(result.ExcelSheets.Materials);
        Assert.Single(result.ExcelSheets.RiskRegister);
    }

    [Fact]
    public async Task GeneratePlanJsonRejectsInvalidAiJson()
    {
        var google = new FakeGoogleAIClient { NextResult = GoogleAITextResult.Success("Here is the plan, but not JSON.") };
        var service = CreateService(new TestUnitOfWork(), google);

        var response = await service.GeneratePlanJsonAsync(ValidRequest());

        Assert.False(response.IsSuccess);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("invalid planner JSON", response.ErrorMessage);
    }

    [Fact]
    public async Task GeneratePlanJsonChecksOptionalProjectAccess()
    {
        var uow = new TestUnitOfWork();
        uow.ProjectRecords.Add(new Project { ProjectId = 10, ProjectName = "Other PM Project", PMUserID = 999 });
        var service = CreateService(uow, claimService: new FakeClaimService(7, Role.PM));

        var request = ValidRequest();
        request.ProjectId = 10;

        var response = await service.GeneratePlanJsonAsync(request);

        Assert.False(response.IsSuccess);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GenerateExcelRejectsMissingPlan()
    {
        var service = CreateService(new TestUnitOfWork());

        var response = await service.GenerateExcelAsync(new GenerateConstructionPlanExcelRequest());

        Assert.False(response.IsSuccess);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("required", response.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateExcelBuildsDownloadableWorkbookFromPlannerJson()
    {
        var service = CreateService(new TestUnitOfWork());
        var plan = JsonSerializer.Deserialize<ConstructionPlanJsonResponse>(
            ValidPlanJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        var response = await service.GenerateExcelAsync(new GenerateConstructionPlanExcelRequest
        {
            Plan = plan,
            FileName = "test-plan"
        });

        Assert.True(response.IsSuccess);
        var file = Assert.IsType<ConstructionPlanExcelFileResponse>(response.Result);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", file.ContentType);
        Assert.StartsWith("test-plan-", file.FileName);
        Assert.EndsWith(".xlsx", file.FileName);
        Assert.NotEmpty(file.Content);

        using var stream = new MemoryStream(file.Content);
        using var workbook = new XLWorkbook(stream);
        Assert.Equal(12, workbook.Worksheets.Count);
        Assert.True(workbook.Worksheets.Contains("Overview"));
        Assert.True(workbook.Worksheets.Contains("Tasks"));
        Assert.Equal("Project Type", workbook.Worksheet("Overview").Cell(2, 2).GetString());
        Assert.Equal("Submit permit", workbook.Worksheet("Tasks").Cell(2, 3).GetString());
    }

    private static AiConstructionPlannerService CreateService(
        TestUnitOfWork uow,
        IGoogleAIClient? google = null,
        IClaimService? claimService = null) =>
        new(
            uow,
            claimService ?? new FakeClaimService(7, Role.PM),
            google ?? new FakeGoogleAIClient { NextResult = GoogleAITextResult.Success(ValidPlanJson) });

    private static GenerateConstructionPlanRequest ValidRequest() => new()
    {
        Answers = new ConstructionPlanAnswersRequest
        {
            ProjectOverview = "3-floor residential house with 250 m2 total floor area",
            LocationAndSite = "District 7, Ho Chi Minh City with narrow alley access",
            Timeline = "Start 2026-10-01 and finish within 8 months",
            BudgetAndQuality = "3.5 billion VND with mid-high quality finishes",
            SpecialRequirements = "Include permits, safety plan, sustainable materials, and supplier planning"
        }
    };

    private const string ValidPlanJson = """
{
  "planId": "plan-001",
  "version": "1.0",
  "generatedAt": "2026-09-01T00:00:00Z",
  "projectSummary": {
    "projectName": "BuildSense Test House",
    "projectType": "Residential",
    "location": "District 7, Ho Chi Minh City",
    "scope": "3 floors, 250 m2",
    "assumptions": ["Quantities require drawing validation"],
    "currency": "VND",
    "estimatedBudget": 3500000000,
    "targetStartDate": "2026-10-01",
    "targetEndDate": "2027-06-01",
    "estimatedDurationDays": 240
  },
  "excelSheets": {
    "overview": [
      { "section": "Project", "item": "Project Type", "value": "Residential", "notes": "Generated from test answers" }
    ],
    "phases": [
      { "phaseId": "P01", "phaseName": "Pre-construction", "description": "Permits and design", "startWeek": 1, "endWeek": 4, "durationDays": 28, "estimatedCost": 100000000, "dependencies": [], "deliverables": ["Permit package"] }
    ],
    "tasks": [
      { "taskId": "T001", "phaseId": "P01", "taskName": "Submit permit", "description": "Submit local permit package", "startWeek": 1, "endWeek": 2, "durationDays": 10, "predecessorTaskIds": [], "responsibleRole": "Project Manager", "estimatedCost": 10000000, "priority": "High", "acceptanceCriteria": "Permit submitted" }
    ],
    "materials": [
      { "materialId": "M001", "phaseId": "P02", "materialName": "Concrete", "specification": "C30", "estimatedQuantity": 50, "unit": "m3", "unitCost": 1500000, "totalCost": 75000000, "neededByWeek": 6, "notes": "Validate with drawings" }
    ],
    "labor": [
      { "laborId": "L001", "phaseId": "P02", "role": "Site supervisor", "estimatedHeadcount": 1, "durationDays": 60, "dailyRate": 1000000, "totalCost": 60000000, "notes": "Required during structure" }
    ],
    "equipment": [
      { "equipmentId": "E001", "phaseId": "P02", "equipmentName": "Concrete pump", "quantity": 1, "durationDays": 5, "dailyRate": 5000000, "totalCost": 25000000, "notes": "Depends on access" }
    ],
    "costPlan": [
      { "costCode": "C001", "category": "Materials", "description": "Concrete and rebar", "estimatedAmount": 500000000, "percentageOfBudget": 14.29, "contingencyAmount": 50000000, "notes": "Planning estimate" }
    ],
    "procurementPlan": [
      { "procurementId": "PR001", "itemName": "Rebar", "sourceType": "Supplier", "requiredByWeek": 5, "leadTimeDays": 14, "orderByWeek": 3, "estimatedCost": 200000000, "riskLevel": "Medium", "notes": "Confirm market price" }
    ],
    "riskRegister": [
      { "riskId": "R001", "riskCategory": "Schedule", "riskDescription": "Permit delay", "probability": "Medium", "impact": "High", "mitigationPlan": "Submit early", "ownerRole": "Project Manager" }
    ],
    "permitChecklist": [
      { "permitId": "PER001", "permitName": "Building permit", "required": true, "targetSubmissionWeek": 1, "targetApprovalWeek": 4, "responsibleRole": "Project Manager", "notes": "Verify local rules" }
    ],
    "safetyPlan": [
      { "safetyId": "S001", "activity": "Excavation", "hazard": "Collapse", "controlMeasure": "Shoring and inspection", "inspectionFrequency": "Daily", "responsibleRole": "Site Supervisor" }
    ],
    "milestones": [
      { "milestoneId": "MS001", "milestoneName": "Foundation complete", "targetWeek": 8, "relatedPhaseId": "P02", "completionCriteria": "Inspected and accepted" }
    ]
  }
}
""";
}
