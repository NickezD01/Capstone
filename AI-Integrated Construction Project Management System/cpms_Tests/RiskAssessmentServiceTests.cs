using cpms_Application.Interfaces;
using cpms_Application.Response.RiskAssessment;
using cpms_Application.Services;
using cpms_Domain;
using cpms_Domain.Models;
using System.Net;

namespace cpms_Tests;

public class RiskAssessmentServiceTests
{
    [Fact]
    public async Task BehindScheduleTaskProducesDeviationAndDelayWarnings()
    {
        var uow = new TestUnitOfWork();
        uow.ProjectRecords.Add(CreateProject());
        // Day 31 of 41: expected ~76%, actual 10%, deadline 10 days out.
        uow.TaskRecords.Add(CreateTask(taskId: 1, startOffset: -30, endOffset: 10, actualPct: 10));
        var service = new RiskAssessmentService(uow, new FakeClaimService(5, Role.PM));

        var response = await service.GetProjectRisksAsync(1);

        Assert.True(response.IsSuccess, response.ErrorMessage);
        var risks = Assert.IsType<ProjectRiskResponse>(response.Result).Risks;
        Assert.Contains(risks, r => r.RiskType == ProjectRiskTypes.ProgressDeviation && r.Severity == RiskSeverity.Warning);
        var delay = Assert.Single(risks, r => r.RiskType == ProjectRiskTypes.ScheduleDelay);
        Assert.Equal(RiskSeverity.Warning, delay.Severity);
        Assert.Equal(1, delay.TaskId);
    }

    [Fact]
    public async Task OverdueTaskProducesCriticalDelayAndOverall()
    {
        var uow = new TestUnitOfWork();
        uow.ProjectRecords.Add(CreateProject());
        uow.TaskRecords.Add(CreateTask(taskId: 1, startOffset: -40, endOffset: -3, actualPct: 50));
        var service = new RiskAssessmentService(uow, new FakeClaimService(5, Role.PM));

        var response = await service.GetProjectRisksAsync(1);

        Assert.True(response.IsSuccess, response.ErrorMessage);
        var risks = Assert.IsType<ProjectRiskResponse>(response.Result).Risks;
        Assert.Contains(risks, r => r.RiskType == ProjectRiskTypes.ScheduleDelay && r.Severity == RiskSeverity.Critical);
        var overall = Assert.Single(risks, r => r.RiskType == ProjectRiskTypes.OverallProjectDelay);
        Assert.Equal(RiskSeverity.Critical, overall.Severity);
    }

    [Fact]
    public async Task StalledTaskProducesWorkItemDelay()
    {
        var uow = new TestUnitOfWork();
        uow.ProjectRecords.Add(CreateProject());
        uow.TaskRecords.Add(CreateTask(taskId: 1, startOffset: -10, endOffset: 20, actualPct: 0));
        var service = new RiskAssessmentService(uow, new FakeClaimService(5, Role.PM));

        var response = await service.GetProjectRisksAsync(1);

        Assert.True(response.IsSuccess, response.ErrorMessage);
        var risks = Assert.IsType<ProjectRiskResponse>(response.Result).Risks;
        Assert.Contains(risks, r => r.RiskType == ProjectRiskTypes.WorkItemDelay);
    }

    [Fact]
    public async Task LowStockProducesMaterialShortage()
    {
        var uow = CreateMaterialFixture(onHand: 10, issued: 0, returned: 0);
        var service = new RiskAssessmentService(uow, new FakeClaimService(5, Role.PM));

        var response = await service.GetProjectRisksAsync(1);

        Assert.True(response.IsSuccess, response.ErrorMessage);
        var risks = Assert.IsType<ProjectRiskResponse>(response.Result).Risks;
        var shortage = Assert.Single(risks, r => r.RiskType == ProjectRiskTypes.MaterialShortage);
        Assert.Equal(90, shortage.Metrics["shortfall"]);
    }

    [Fact]
    public async Task UnfulfilledRequestNearStartProducesAvailabilityWarning()
    {
        var uow = CreateMaterialFixture(onHand: 200, issued: 0, returned: 0, taskStartOffset: -1);
        uow.RequestRecords.Add(new MaterialRequest
        {
            RequestId = 1,
            ProjectId = 1,
            TaskId = 1,
            Status = MaterialRequestStatuses.Pending
        });
        var service = new RiskAssessmentService(uow, new FakeClaimService(5, Role.PM));

        var response = await service.GetProjectRisksAsync(1);

        Assert.True(response.IsSuccess, response.ErrorMessage);
        var risks = Assert.IsType<ProjectRiskResponse>(response.Result).Risks;
        Assert.Contains(risks, r => r.RiskType == ProjectRiskTypes.MaterialAvailability);
    }

    [Fact]
    public async Task SpendNearBudgetProducesBudgetWarning()
    {
        var uow = new TestUnitOfWork();
        uow.ProjectRecords.Add(CreateProject(budget: 1000));
        uow.TaskRecords.Add(CreateTask(taskId: 1, startOffset: -30, endOffset: 30, actualPct: 55));
        uow.MaterialBudgetTransactionRecords.Add(new MaterialBudgetTransaction
        {
            Id = 1,
            ProjectId = 1,
            RequestId = 1,
            TransactionType = MaterialBudgetTransactionTypes.IssueDebit,
            Amount = 850,
            PerformedByUserId = 10,
            CreatedAt = DateTime.UtcNow
        });
        var service = new RiskAssessmentService(uow, new FakeClaimService(5, Role.PM));

        var response = await service.GetProjectRisksAsync(1);

        Assert.True(response.IsSuccess, response.ErrorMessage);
        var risks = Assert.IsType<ProjectRiskResponse>(response.Result).Risks;
        var budget = Assert.Single(risks, r => r.RiskType == ProjectRiskTypes.BudgetRisk);
        Assert.Equal(RiskSeverity.Warning, budget.Severity);
    }

    [Fact]
    public async Task OnTrackProjectProducesNoRisks()
    {
        var uow = new TestUnitOfWork();
        uow.ProjectRecords.Add(CreateProject(budget: 100000));
        // Actual 55 vs expected ~51: ahead, deadline far, healthy pace.
        uow.TaskRecords.Add(CreateTask(taskId: 1, startOffset: -30, endOffset: 30, actualPct: 55));
        var service = new RiskAssessmentService(uow, new FakeClaimService(5, Role.PM));

        var response = await service.GetProjectRisksAsync(1);

        Assert.True(response.IsSuccess, response.ErrorMessage);
        Assert.Empty(Assert.IsType<ProjectRiskResponse>(response.Result).Risks);
    }

    [Fact]
    public async Task RiskAccessIsRestrictedToAdminAndOwningPm()
    {
        var uow = new TestUnitOfWork();
        uow.ProjectRecords.Add(CreateProject());

        var missing = await new RiskAssessmentService(uow, new FakeClaimService(1, Role.ADMIN))
            .GetProjectRisksAsync(99);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        var stranger = await new RiskAssessmentService(uow, new FakeClaimService(6, Role.PM))
            .GetProjectRisksAsync(1);
        Assert.Equal(HttpStatusCode.Forbidden, stranger.StatusCode);

        var warehouseManager = await new RiskAssessmentService(uow, new FakeClaimService(10, Role.WAREHOUSE_MANAGER))
            .GetProjectRisksAsync(1);
        Assert.Equal(HttpStatusCode.Forbidden, warehouseManager.StatusCode);

        var admin = await new RiskAssessmentService(uow, new FakeClaimService(1, Role.ADMIN))
            .GetProjectRisksAsync(1);
        Assert.True(admin.IsSuccess, admin.ErrorMessage);
    }

    [Fact]
    public async Task RecommendActionsReturnsEmptyWithoutAiCallWhenNoRisks()
    {
        var uow = new TestUnitOfWork();
        uow.ProjectRecords.Add(CreateProject(budget: 100000));
        uow.TaskRecords.Add(CreateTask(taskId: 1, startOffset: -30, endOffset: 30, actualPct: 55));
        var google = new FakeGoogleAIClient();
        var service = new RiskAssessmentService(uow, new FakeClaimService(5, Role.PM), googleAIClient: google);

        var response = await service.RecommendActionsAsync(1);

        Assert.True(response.IsSuccess, response.ErrorMessage);
        Assert.Empty(Assert.IsType<RecommendRiskActionsResponse>(response.Result).Actions);
        Assert.Equal(0, google.CallCount);
    }

    [Fact]
    public async Task RecommendActionsMapsValidAiResponse()
    {
        var uow = new TestUnitOfWork();
        uow.ProjectRecords.Add(CreateProject());
        uow.TaskRecords.Add(CreateTask(taskId: 1, startOffset: -30, endOffset: 10, actualPct: 10));
        var google = new FakeGoogleAIClient { NextResult = GoogleAITextResult.Success(ValidActionsJson) };
        var service = new RiskAssessmentService(uow, new FakeClaimService(5, Role.PM), googleAIClient: google);

        var response = await service.RecommendActionsAsync(1);

        Assert.True(response.IsSuccess, response.ErrorMessage);
        var actions = Assert.IsType<RecommendRiskActionsResponse>(response.Result).Actions;
        Assert.Equal(2, actions.Count);
        Assert.Equal(1, actions[0].Priority);
        Assert.Equal("Crash the schedule", actions[0].Title);
        Assert.Equal("PM", actions[0].OwnerRole);
        Assert.Contains("SCHEDULE_DELAY", actions[0].RelatedRiskTypes);
        Assert.Equal(1, google.CallCount);
    }

    [Fact]
    public async Task RecommendActionsRejectsInvalidJsonAndContractViolations()
    {
        var invalidUow = new TestUnitOfWork();
        invalidUow.ProjectRecords.Add(CreateProject());
        invalidUow.TaskRecords.Add(CreateTask(taskId: 1, startOffset: -30, endOffset: 10, actualPct: 10));
        var invalid = await new RiskAssessmentService(invalidUow, new FakeClaimService(5, Role.PM),
                googleAIClient: new FakeGoogleAIClient { NextResult = GoogleAITextResult.Success("not json") })
            .RecommendActionsAsync(1);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        var badContractUow = new TestUnitOfWork();
        badContractUow.ProjectRecords.Add(CreateProject());
        badContractUow.TaskRecords.Add(CreateTask(taskId: 1, startOffset: -30, endOffset: 10, actualPct: 10));
        var badContract = await new RiskAssessmentService(badContractUow, new FakeClaimService(5, Role.PM),
                googleAIClient: new FakeGoogleAIClient { NextResult = GoogleAITextResult.Success("""{ "actions": [{ "priority": 9, "title": "X", "ownerRole": "PM", "relatedRiskTypes": ["SCHEDULE_DELAY"] }] }""") })
            .RecommendActionsAsync(1);
        Assert.Equal(HttpStatusCode.BadRequest, badContract.StatusCode);
    }

    [Fact]
    public async Task RecommendActionsRestrictedToOwningPm()
    {
        var uow = new TestUnitOfWork();
        uow.ProjectRecords.Add(CreateProject());

        var missing = await new RiskAssessmentService(uow, new FakeClaimService(5, Role.PM),
                googleAIClient: new FakeGoogleAIClient())
            .RecommendActionsAsync(99);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        var admin = await new RiskAssessmentService(uow, new FakeClaimService(1, Role.ADMIN),
                googleAIClient: new FakeGoogleAIClient())
            .RecommendActionsAsync(1);
        Assert.Equal(HttpStatusCode.Forbidden, admin.StatusCode);

        var stranger = await new RiskAssessmentService(uow, new FakeClaimService(6, Role.PM),
                googleAIClient: new FakeGoogleAIClient())
            .RecommendActionsAsync(1);
        Assert.Equal(HttpStatusCode.Forbidden, stranger.StatusCode);
    }

    private const string ValidActionsJson = """
{
  "actions": [
    { "priority": 2, "title": "Expedite steel delivery", "detail": "Confirm supplier lead time.", "ownerRole": "WAREHOUSE_MANAGER", "relatedRiskTypes": ["MATERIAL_SHORTAGE"] },
    { "priority": 1, "title": "Crash the schedule", "detail": "Add a second crew to Excavate.", "ownerRole": "pm", "relatedRiskTypes": ["schedule_delay"] }
  ]
}
""";

    private static Project CreateProject(decimal budget = 100000) => new()
    {
        ProjectId = 1,
        ProjectName = "P",
        PMUserID = 5,
        Status = ProjectStatus.IN_PROGRESS,
        StartDate = DateTime.UtcNow.Date.AddDays(-30),
        BaselineStart = DateTime.UtcNow.Date.AddDays(-30),
        BaselineEnd = DateTime.UtcNow.Date.AddDays(30),
        TotalProjectBudget = budget
    };

    private static TaskItem CreateTask(int taskId, int startOffset, int endOffset, decimal actualPct) => new()
    {
        TaskId = taskId,
        ProjectId = 1,
        PhaseId = 1,
        PhaseName = "P",
        TaskName = $"T{taskId}",
        AssignedToUserID = 5,
        PlannedBudget = 1000,
        ActualCost = 0,
        BaselineStart = DateTime.UtcNow.Date.AddDays(startOffset),
        BaselineEnd = DateTime.UtcNow.Date.AddDays(endOffset),
        ActualProgressPct = actualPct,
        Status = cpms_Domain.Models.TaskStatus.IN_PROGRESS
    };

    private static TestUnitOfWork CreateMaterialFixture(
        decimal onHand, decimal issued, decimal returned, int taskStartOffset = -30)
    {
        var uow = new TestUnitOfWork();
        uow.ProjectRecords.Add(CreateProject());
        uow.TaskRecords.Add(CreateTask(taskId: 1, startOffset: taskStartOffset, endOffset: 30, actualPct: 55));
        var material = new Material { MaterialId = 1, MaterialName = "Cement", DefaultUnit = "bag" };
        var variant = new MaterialVariant { VariantId = 1, MaterialId = 1, Material = material, VariantName = "Type I", Unit = "bag", IsActive = true };
        var request = new MaterialRequest
        {
            RequestId = 1,
            ProjectId = 1,
            TaskId = 1,
            Status = MaterialRequestStatuses.Issued
        };
        uow.WarehouseRecords.Add(new Warehouse { WarehouseId = 1, WarehouseName = "W", ManagerId = 10, IsActive = true });
        uow.MaterialRecords.Add(material);
        uow.VariantRecords.Add(variant);
        uow.RequirementRecords.Add(new TaskMaterialRequirement { Id = 1, TaskId = 1, VariantId = 1, GrossQuantityRequired = 100 });
        uow.InventoryRecords.Add(new InventoryRecord
        {
            InventoryId = 1,
            WarehouseId = 1,
            VariantId = 1,
            QuantityOnHand = onHand
        });
        if (issued > 0)
        {
            uow.RequisitionRecords.Add(new MaterialRequisition
            {
                ItemId = 1,
                RequestId = 1,
                MaterialRequest = request,
                VariantId = 1,
                Quantity = 100,
                ApprovedQuantity = 100,
                IssuedQuantity = issued
            });
        }
        if (returned > 0)
        {
            uow.MaterialReturnRecords.Add(new MaterialReturn
            {
                ReturnId = 1,
                MaterialRequestId = 1,
                MaterialRequest = request,
                WarehouseId = 1,
                VariantId = 1,
                Quantity = returned,
                RecordedByUserId = 10,
                ReturnedAt = DateTime.UtcNow
            });
        }
        return uow;
    }
}
