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

    [Fact]
    public async Task GenerateExcelChecksOptionalProjectAccess()
    {
        var uow = new TestUnitOfWork();
        uow.ProjectRecords.Add(new Project { ProjectId = 10, ProjectName = "Other PM Project", PMUserID = 999 });
        var service = CreateService(uow, claimService: new FakeClaimService(7, Role.PM));

        var response = await service.GenerateExcelAsync(new GenerateConstructionPlanExcelRequest
        {
            ProjectId = 10,
            Plan = new ConstructionPlanJsonResponse()
        });

        Assert.False(response.IsSuccess);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GeneratePhasesPreviewRequiresOwningPm()
    {
        var uow = new TestUnitOfWork();
        uow.ProjectRecords.Add(CreateOwnedProject(pmUserId: 999));
        var service = CreateService(uow, claimService: new FakeClaimService(7, Role.PM));

        var response = await service.GenerateProjectPhasesPreviewAsync(1, new GenerateProjectAiPhasesRequest
        {
            Answers = ValidAnswers()
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(uow.PhaseRecords);
    }

    [Fact]
    public async Task GeneratePhasesPreviewReturnsTempIdsWithoutPersisting()
    {
        var uow = new TestUnitOfWork();
        uow.ProjectRecords.Add(CreateOwnedProject());
        var service = CreateService(uow);

        var response = await service.GenerateProjectPhasesPreviewAsync(1, new GenerateProjectAiPhasesRequest
        {
            Answers = ValidAnswers()
        });

        Assert.True(response.IsSuccess, response.ErrorMessage);
        var preview = Assert.IsType<ProjectAiPlanPreviewResponse>(response.Result);
        var phase = Assert.Single(preview.Phases);
        Assert.Equal("PH-P01", phase.TempId);
        Assert.Equal("P01", phase.AiKey);
        Assert.Equal("Pre-construction", phase.Name);
        Assert.Equal(new DateTime(2026, 10, 1), phase.BaselineStart);
        Assert.Equal(new DateTime(2026, 10, 28), phase.BaselineEnd);
        Assert.Empty(preview.Tasks);
        Assert.Empty(uow.PhaseRecords);
        Assert.Empty(uow.TaskRecords);
    }

    [Fact]
    public async Task GenerateTasksPreviewRequiresPhaseSource()
    {
        var uow = new TestUnitOfWork();
        uow.ProjectRecords.Add(CreateOwnedProject());
        var service = CreateService(uow);

        var response = await service.GenerateProjectTasksPreviewAsync(1, new GenerateProjectAiTasksRequest
        {
            Answers = ValidAnswers()
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GenerateTasksPreviewMapsTasksToEchoedPhaseTemps()
    {
        var uow = new TestUnitOfWork();
        uow.ProjectRecords.Add(CreateOwnedProject());
        var service = CreateService(uow);

        var response = await service.GenerateProjectTasksPreviewAsync(1, new GenerateProjectAiTasksRequest
        {
            Answers = ValidAnswers(),
            Phases = new List<AiPhaseProposalResponse>
            {
                new()
                {
                    TempId = "MY-PH",
                    AiKey = "P01",
                    Name = "Renamed by PM",
                    SequenceOrder = 0,
                    BaselineStart = new DateTime(2026, 10, 1),
                    BaselineEnd = new DateTime(2026, 10, 31)
                }
            }
        });

        Assert.True(response.IsSuccess, response.ErrorMessage);
        var preview = Assert.IsType<ProjectAiPlanPreviewResponse>(response.Result);
        Assert.Equal("MY-PH", Assert.Single(preview.Phases).TempId);
        var task = Assert.Single(preview.Tasks);
        Assert.Equal("MY-PH", task.PhaseTempId);
        Assert.Null(task.PhaseId);
        Assert.Equal("Submit permit", task.TaskName);
        Assert.Equal(10000000, task.PlannedBudget);
        Assert.Empty(uow.TaskRecords);
    }

    [Fact]
    public async Task GenerateTasksPreviewForExistingPhase()
    {
        var uow = new TestUnitOfWork();
        uow.ProjectRecords.Add(CreateOwnedProject());
        uow.PhaseRecords.Add(new Phase
        {
            PhaseId = 3,
            ProjectId = 1,
            Name = "Pre-construction",
            SequenceOrder = 0,
            BaselineStart = new DateTime(2026, 10, 1),
            BaselineEnd = new DateTime(2026, 12, 31)
        });
        var service = CreateService(uow);

        var response = await service.GenerateProjectTasksPreviewAsync(1, new GenerateProjectAiTasksRequest
        {
            Answers = ValidAnswers(),
            PhaseId = 3
        });

        Assert.True(response.IsSuccess, response.ErrorMessage);
        var preview = Assert.IsType<ProjectAiPlanPreviewResponse>(response.Result);
        var task = Assert.Single(preview.Tasks);
        Assert.Equal(3, task.PhaseId);
        Assert.Null(task.PhaseTempId);
    }

    [Fact]
    public async Task ConfirmPersistsEditedPreviewInOneTransaction()
    {
        var uow = new TestUnitOfWork();
        uow.ProjectRecords.Add(CreateOwnedProject());
        var service = CreateService(uow);

        var response = await service.ConfirmProjectAiPlanAsync(1, new ConfirmProjectAiPlanRequest
        {
            Phases = new List<AiPhaseProposalRequest>
            {
                new()
                {
                    TempId = "PH-1",
                    Name = "Edited phase",
                    SequenceOrder = 0,
                    BaselineStart = new DateTime(2026, 10, 1),
                    BaselineEnd = new DateTime(2026, 10, 31)
                }
            },
            Tasks = new List<AiTaskProposalRequest>
            {
                new()
                {
                    TempId = "TSK-1",
                    PhaseTempId = "PH-1",
                    TaskName = "Edited task",
                    BaselineStart = new DateTime(2026, 10, 1),
                    BaselineEnd = new DateTime(2026, 10, 14),
                    PlannedBudget = 5000000
                }
            }
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = Assert.IsType<ConfirmProjectAiPlanResponse>(response.Result);
        var phase = Assert.Single(uow.PhaseRecords);
        Assert.Equal("Edited phase", phase.Name);
        Assert.Equal("PH-1", Assert.Single(result.Phases).TempId);
        Assert.Equal(phase.PhaseId, result.Phases[0].PhaseId);
        var task = Assert.Single(uow.TaskRecords);
        Assert.Equal("Edited task", task.TaskName);
        Assert.Equal(phase.PhaseId, task.PhaseId);
        Assert.Equal("Edited phase", task.PhaseName);
        Assert.Equal(7, task.AssignedToUserID);
        Assert.Equal(phase.PhaseId, result.Tasks[0].PhaseId);
    }

    [Fact]
    public async Task ConfirmResolvesExistingPhaseIdWithoutCreatingPhases()
    {
        var uow = new TestUnitOfWork();
        uow.ProjectRecords.Add(CreateOwnedProject());
        uow.PhaseRecords.Add(new Phase
        {
            PhaseId = 3,
            ProjectId = 1,
            Name = "Existing",
            SequenceOrder = 0,
            BaselineStart = new DateTime(2026, 10, 1),
            BaselineEnd = new DateTime(2026, 12, 31)
        });
        var service = CreateService(uow);

        var response = await service.ConfirmProjectAiPlanAsync(1, new ConfirmProjectAiPlanRequest
        {
            Tasks = new List<AiTaskProposalRequest>
            {
                new()
                {
                    TempId = "TSK-1",
                    PhaseId = 3,
                    TaskName = "Extra task",
                    BaselineStart = new DateTime(2026, 10, 1),
                    BaselineEnd = new DateTime(2026, 10, 14),
                    PlannedBudget = 1000
                }
            }
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Single(uow.PhaseRecords);
        var task = Assert.Single(uow.TaskRecords);
        Assert.Equal(3, task.PhaseId);
        Assert.Equal("Existing", task.PhaseName);
    }

    [Fact]
    public async Task ConfirmRejectsNonOwningPmClosedProjectAndInvalidProposals()
    {
        var openUow = new TestUnitOfWork();
        openUow.ProjectRecords.Add(CreateOwnedProject(pmUserId: 999));
        var forbidden = await CreateService(openUow, claimService: new FakeClaimService(7, Role.PM))
            .ConfirmProjectAiPlanAsync(1, new ConfirmProjectAiPlanRequest
            {
                Tasks = new List<AiTaskProposalRequest>
                {
                    new() { TempId = "TSK-1", PhaseId = 1, TaskName = "T", BaselineStart = new DateTime(2026, 10, 1), BaselineEnd = new DateTime(2026, 10, 2) }
                }
            });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var closedUow = new TestUnitOfWork();
        closedUow.ProjectRecords.Add(CreateOwnedProject(status: ProjectStatus.COMPLETED));
        var closed = await CreateService(closedUow)
            .ConfirmProjectAiPlanAsync(1, new ConfirmProjectAiPlanRequest
            {
                Phases = new List<AiPhaseProposalRequest>
                {
                    new() { TempId = "PH-1", Name = "P", BaselineStart = new DateTime(2026, 10, 1), BaselineEnd = new DateTime(2026, 10, 2) }
                }
            });
        Assert.Equal(HttpStatusCode.Conflict, closed.StatusCode);

        var dupUow = new TestUnitOfWork();
        dupUow.ProjectRecords.Add(CreateOwnedProject());
        var duplicate = await CreateService(dupUow)
            .ConfirmProjectAiPlanAsync(1, new ConfirmProjectAiPlanRequest
            {
                Phases = new List<AiPhaseProposalRequest>
                {
                    new() { TempId = "PH-1", Name = "Same", BaselineStart = new DateTime(2026, 10, 1), BaselineEnd = new DateTime(2026, 10, 2) },
                    new() { TempId = "PH-1", Name = "Other", BaselineStart = new DateTime(2026, 10, 1), BaselineEnd = new DateTime(2026, 10, 2) }
                }
            });
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Empty(dupUow.PhaseRecords);

        var unknownUow = new TestUnitOfWork();
        unknownUow.ProjectRecords.Add(CreateOwnedProject());
        var unknown = await CreateService(unknownUow)
            .ConfirmProjectAiPlanAsync(1, new ConfirmProjectAiPlanRequest
            {
                Phases = new List<AiPhaseProposalRequest>
                {
                    new() { TempId = "PH-1", Name = "P", BaselineStart = new DateTime(2026, 10, 1), BaselineEnd = new DateTime(2026, 10, 2) }
                },
                Tasks = new List<AiTaskProposalRequest>
                {
                    new() { TempId = "TSK-1", PhaseTempId = "NOPE", TaskName = "T", BaselineStart = new DateTime(2026, 10, 1), BaselineEnd = new DateTime(2026, 10, 2) }
                }
            });
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Empty(unknownUow.PhaseRecords);
        Assert.Empty(unknownUow.TaskRecords);
    }

    [Fact]
    public async Task ConfirmRejectsBudgetOverflow()
    {
        var uow = new TestUnitOfWork();
        var project = CreateOwnedProject();
        project.TotalProjectBudget = 1000000;
        uow.ProjectRecords.Add(project);
        var service = CreateService(uow);

        var response = await service.ConfirmProjectAiPlanAsync(1, new ConfirmProjectAiPlanRequest
        {
            Phases = new List<AiPhaseProposalRequest>
            {
                new() { TempId = "PH-1", Name = "P", BaselineStart = new DateTime(2026, 10, 1), BaselineEnd = new DateTime(2026, 10, 2) }
            },
            Tasks = new List<AiTaskProposalRequest>
            {
                new() { TempId = "TSK-1", PhaseTempId = "PH-1", TaskName = "T", BaselineStart = new DateTime(2026, 10, 1), BaselineEnd = new DateTime(2026, 10, 2), PlannedBudget = 5000000 }
            }
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Empty(uow.PhaseRecords);
        Assert.Empty(uow.TaskRecords);
    }

    [Fact]
    public async Task GeneratePhasesPreviewAcceptsBrief()
    {
        var uow = new TestUnitOfWork();
        uow.ProjectRecords.Add(CreateOwnedProject());
        var google = new FakeGoogleAIClient { NextResult = GoogleAITextResult.Success(ValidPlanJson) };
        var service = CreateService(uow, google);

        var response = await service.GenerateProjectPhasesPreviewAsync(1, new GenerateProjectAiPhasesRequest
        {
            Brief = ValidBrief()
        });

        Assert.True(response.IsSuccess, response.ErrorMessage);
        Assert.Contains("Structured project brief", google.LastInput, StringComparison.Ordinal);
        Assert.Contains("Floor area: 180", google.LastInput, StringComparison.Ordinal);
        Assert.Contains("Target start date: 2026-10-01", google.LastInput, StringComparison.Ordinal);
        Assert.Contains("Target end date: 2027-06-01", google.LastInput, StringComparison.Ordinal);
        Assert.DoesNotContain("Expected duration", google.LastInput, StringComparison.Ordinal);
        var preview = Assert.IsType<ProjectAiPlanPreviewResponse>(response.Result);
        Assert.Equal("PH-P01", Assert.Single(preview.Phases).TempId);
        Assert.Empty(uow.PhaseRecords);
    }

    [Fact]
    public async Task GeneratePhasesPreviewRejectsInvalidBrief()
    {
        var uow = new TestUnitOfWork();
        uow.ProjectRecords.Add(CreateOwnedProject());
        var service = CreateService(uow);

        var response = await service.GenerateProjectPhasesPreviewAsync(1, new GenerateProjectAiPhasesRequest
        {
            Brief = new AiProjectBriefRequest
            {
                ProjectType = "House",
                FloorAreaM2 = 0,
                NumberOfFloors = 2,
                StartDate = new DateTime(2026, 10, 1),
                EndDate = new DateTime(2027, 6, 1)
            }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(uow.PhaseRecords);
    }

    [Fact]
    public async Task GeneratePhasesPreviewRejectsEndBeforeStart()
    {
        var uow = new TestUnitOfWork();
        uow.ProjectRecords.Add(CreateOwnedProject());
        var brief = ValidBrief();
        brief.EndDate = new DateTime(2026, 9, 1);
        var service = CreateService(uow);

        var response = await service.GenerateProjectPhasesPreviewAsync(1, new GenerateProjectAiPhasesRequest
        {
            Brief = brief
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(uow.PhaseRecords);
    }

    [Fact]
    public async Task GenerateTasksPreviewAcceptsBrief()
    {
        var uow = new TestUnitOfWork();
        uow.ProjectRecords.Add(CreateOwnedProject());
        var service = CreateService(uow);

        var response = await service.GenerateProjectTasksPreviewAsync(1, new GenerateProjectAiTasksRequest
        {
            Brief = ValidBrief(),
            Phases = new List<AiPhaseProposalResponse>
            {
                new()
                {
                    TempId = "MY-PH",
                    AiKey = "P01",
                    Name = "Renamed by PM",
                    SequenceOrder = 0,
                    BaselineStart = new DateTime(2026, 10, 1),
                    BaselineEnd = new DateTime(2026, 10, 31)
                }
            }
        });

        Assert.True(response.IsSuccess, response.ErrorMessage);
        var task = Assert.Single(Assert.IsType<ProjectAiPlanPreviewResponse>(response.Result).Tasks);
        Assert.Equal("MY-PH", task.PhaseTempId);
        Assert.Empty(uow.TaskRecords);
    }

    [Fact]
    public async Task BriefWinsOverAnswers()
    {
        var uow = new TestUnitOfWork();
        uow.ProjectRecords.Add(CreateOwnedProject());
        var google = new FakeGoogleAIClient { NextResult = GoogleAITextResult.Success(ValidPlanJson) };
        var service = CreateService(uow, google);

        var response = await service.GenerateProjectPhasesPreviewAsync(1, new GenerateProjectAiPhasesRequest
        {
            Answers = ValidRequest().Answers,
            Brief = ValidBrief()
        });

        Assert.True(response.IsSuccess, response.ErrorMessage);
        Assert.Contains("Structured project brief", google.LastInput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CompleteProposesOnlyRemainingWork()
    {
        var uow = new TestUnitOfWork();
        uow.ProjectRecords.Add(CreateOwnedProject());
        uow.PhaseRecords.Add(new Phase
        {
            PhaseId = 3,
            ProjectId = 1,
            Name = "Pre-construction",
            SequenceOrder = 0,
            BaselineStart = new DateTime(2026, 10, 1),
            BaselineEnd = new DateTime(2026, 10, 31)
        });
        uow.TaskRecords.Add(new TaskItem
        {
            TaskId = 7,
            ProjectId = 1,
            PhaseId = 3,
            PhaseName = "Pre-construction",
            TaskName = "Submit permit",
            BaselineStart = new DateTime(2026, 10, 1),
            BaselineEnd = new DateTime(2026, 10, 14)
        });
        var google = new FakeGoogleAIClient { NextResult = GoogleAITextResult.Success(CompletionPlanJson) };
        var service = CreateService(uow, google);

        var response = await service.CompleteProjectAiPlanAsync(1, new CompleteProjectAiPlanRequest
        {
            Brief = ValidBrief()
        });

        Assert.True(response.IsSuccess, response.ErrorMessage);
        Assert.Contains("Completing an existing plan", google.LastInput, StringComparison.Ordinal);
        var preview = Assert.IsType<ProjectAiPlanPreviewResponse>(response.Result);
        var newPhase = Assert.Single(preview.Phases, p => !string.IsNullOrWhiteSpace(p.TempId));
        Assert.Equal("PH-P02", newPhase.TempId);
        Assert.Equal("Structure", newPhase.Name);
        var reference = Assert.Single(preview.Phases, p => p.PhaseId == 3);
        Assert.Equal(string.Empty, reference.TempId);
        var newTask = Assert.Single(preview.Tasks, t => t.PhaseTempId == "PH-P02");
        Assert.Equal("Pour columns", newTask.TaskName);
        var existingTask = Assert.Single(preview.Tasks, t => t.PhaseId == 3);
        Assert.Equal("Extra permit copy", existingTask.TaskName);
        Assert.Equal(3, preview.Warnings.Count);
        Assert.DoesNotContain(uow.PhaseRecords, p => p.PhaseId != 3);
    }

    [Fact]
    public async Task CompleteRequiresOwningPmAndInput()
    {
        var uow = new TestUnitOfWork();
        uow.ProjectRecords.Add(CreateOwnedProject(pmUserId: 999));

        var forbidden = await CreateService(uow, claimService: new FakeClaimService(7, Role.PM))
            .CompleteProjectAiPlanAsync(1, new CompleteProjectAiPlanRequest { Brief = ValidBrief() });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var owned = new TestUnitOfWork();
        owned.ProjectRecords.Add(CreateOwnedProject());

        var missing = await CreateService(owned)
            .CompleteProjectAiPlanAsync(99, new CompleteProjectAiPlanRequest { Brief = ValidBrief() });
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        var empty = await CreateService(owned)
            .CompleteProjectAiPlanAsync(1, new CompleteProjectAiPlanRequest());
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
    }

    private static AiProjectBriefRequest ValidBrief() => new()
    {
        ProjectType = "House",
        FloorAreaM2 = 180,
        NumberOfFloors = 2,
        StartDate = new DateTime(2026, 10, 1),
        EndDate = new DateTime(2027, 6, 1),
        Budget = 3500000000,
        SpecialRequirements = "Include permits and safety plan"
    };

    private static Project CreateOwnedProject(int pmUserId = 7, ProjectStatus status = ProjectStatus.IN_PROGRESS) => new()
    {
        ProjectId = 1,
        ProjectName = "P",
        PMUserID = pmUserId,
        Status = status,
        StartDate = new DateTime(2026, 10, 1),
        BaselineStart = new DateTime(2026, 10, 1),
        BaselineEnd = new DateTime(2027, 6, 1),
        TotalProjectBudget = 50000000
    };

    private static ConstructionPlanAnswersRequest ValidAnswers() => new()
    {
        ProjectOverview = "3-floor residential house with 250 m2 total floor area",
        LocationAndSite = "District 7, Ho Chi Minh City with narrow alley access",
        Timeline = "Start 2026-10-01 and finish within 8 months",
        BudgetAndQuality = "3.5 billion VND with mid-high quality finishes",
        SpecialRequirements = "Include permits, safety plan, sustainable materials, and supplier planning"
    };

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

    private const string CompletionPlanJson = """
{
  "planId": "plan-002",
  "version": "1.0",
  "generatedAt": "2026-09-01T00:00:00Z",
  "projectSummary": {
    "projectName": "BuildSense Test House",
    "projectType": "Residential",
    "location": "District 7, Ho Chi Minh City",
    "scope": "3 floors, 250 m2",
    "assumptions": [],
    "currency": "VND",
    "estimatedBudget": 3500000000,
    "targetStartDate": "2026-10-01",
    "targetEndDate": "2027-06-01",
    "estimatedDurationDays": 240
  },
  "excelSheets": {
    "overview": [
      { "section": "Project", "item": "Project Type", "value": "Residential", "notes": "" }
    ],
    "phases": [
      { "phaseId": "P01", "phaseName": "Pre-construction", "description": "Permits and design", "startWeek": 1, "endWeek": 4, "durationDays": 28, "estimatedCost": 100000000, "dependencies": [], "deliverables": [] },
      { "phaseId": "P02", "phaseName": "Structure", "description": "Frame and floors", "startWeek": 5, "endWeek": 8, "durationDays": 28, "estimatedCost": 500000000, "dependencies": [], "deliverables": [] }
    ],
    "tasks": [
      { "taskId": "T001", "phaseId": "P01", "taskName": "Submit permit", "description": "Already done", "startWeek": 1, "endWeek": 2, "durationDays": 10, "predecessorTaskIds": [], "responsibleRole": "Project Manager", "estimatedCost": 10000000, "priority": "High", "acceptanceCriteria": "Permit submitted" },
      { "taskId": "T002", "phaseId": "P02", "taskName": "Pour columns", "description": "Ground floor columns", "startWeek": 5, "endWeek": 6, "durationDays": 10, "predecessorTaskIds": [], "responsibleRole": "Site Supervisor", "estimatedCost": 20000000, "priority": "High", "acceptanceCriteria": "Columns cured" },
      { "taskId": "T003", "phaseId": "P01", "taskName": "Extra permit copy", "description": "Additional paperwork", "startWeek": 2, "endWeek": 3, "durationDays": 5, "predecessorTaskIds": [], "responsibleRole": "Project Manager", "estimatedCost": 1000000, "priority": "Low", "acceptanceCriteria": "Filed" },
      { "taskId": "T004", "phaseId": "P99", "taskName": "Orphan task", "description": "Nowhere to go", "startWeek": 2, "endWeek": 3, "durationDays": 5, "predecessorTaskIds": [], "responsibleRole": "Project Manager", "estimatedCost": 1000000, "priority": "Low", "acceptanceCriteria": "None" }
    ],
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
""";

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

internal sealed class FakeGoogleAIClient : IGoogleAIClient
{
    public int CallCount { get; private set; }
    public string? LastInput { get; private set; }
    public GoogleAITextResult NextResult { get; set; } = GoogleAITextResult.Success("Test reply");

    public Task<GoogleAITextResult> GenerateTextAsync(string systemInstruction, string input)
    {
        CallCount++;
        LastInput = input;
        return Task.FromResult(NextResult);
    }
}
