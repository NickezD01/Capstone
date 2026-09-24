using AutoMapper;
using cpms_Application.MyMapper;
using cpms_Application.Request.Phase;
using cpms_Application.Response.Phase;
using cpms_Application.Services;
using cpms_Domain.Models;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;

namespace cpms_Tests;

public class PhaseServiceTests
{
    [Fact]
    public async Task OwningPmCanCreatePhaseWithinProjectDates()
    {
        var uow = CreateUnitOfWork();
        var service = CreateService(uow, 5, Role.PM);

        var response = await service.CreatePhaseAsync(1, new CreatePhaseRequest
        {
            Name = "Foundation",
            Description = "Groundwork",
            SequenceOrder = 1,
            BaselineStart = new DateTime(2026, 9, 1),
            BaselineEnd = new DateTime(2026, 9, 30),
            WorkCategoryId = 1
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var phase = Assert.IsType<PhaseResponse>(response.Result);
        Assert.Equal("Foundation", phase.Name);
        Assert.Single(uow.PhaseRecords);
    }

    [Fact]
    public async Task NonOwningPmCannotCreatePhase()
    {
        var service = CreateService(CreateUnitOfWork(), 6, Role.PM);

        var response = await service.CreatePhaseAsync(1, ValidCreateRequest("Foundation"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PhaseDatesMustStayInsideProjectBaseline()
    {
        var service = CreateService(CreateUnitOfWork(), 5, Role.PM);

        var response = await service.CreatePhaseAsync(1, new CreatePhaseRequest
        {
            Name = "Foundation",
            SequenceOrder = 1,
            BaselineStart = new DateTime(2026, 8, 31),
            BaselineEnd = new DateTime(2026, 9, 30),
            WorkCategoryId = 1
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DuplicatePhaseNameIsRejectedWithinProject()
    {
        var uow = CreateUnitOfWork();
        uow.PhaseRecords.Add(new Phase
        {
            PhaseId = 1,
            ProjectId = 1,
            Name = "Foundation",
            SequenceOrder = 1,
            BaselineStart = new DateTime(2026, 9, 1),
            BaselineEnd = new DateTime(2026, 9, 30)
        });
        var service = CreateService(uow, 5, Role.PM);

        var response = await service.CreatePhaseAsync(1, ValidCreateRequest(" foundation "));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ProjectPhaseListIsOrderedBySequenceThenName()
    {
        var uow = CreateUnitOfWork();
        uow.PhaseRecords.AddRange(new[]
        {
            new Phase { PhaseId = 1, ProjectId = 1, Name = "Zeta", SequenceOrder = 2 },
            new Phase { PhaseId = 2, ProjectId = 1, Name = "Alpha", SequenceOrder = 1 },
            new Phase { PhaseId = 3, ProjectId = 1, Name = "Beta", SequenceOrder = 1 }
        });
        var service = CreateService(uow, 5, Role.PM);

        var response = await service.GetPhasesByProjectAsync(1);

        var phases = Assert.IsType<List<PhaseResponse>>(response.Result);
        Assert.Equal(new[] { "Alpha", "Beta", "Zeta" }, phases.Select(phase => phase.Name));
    }

    [Fact]
    public async Task MissingProjectReturnsNotFound()
    {
        var response = await CreateService(CreateUnitOfWork(), 5, Role.PM)
            .CreatePhaseAsync(99, ValidCreateRequest("Foundation"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AdminCanReadButCannotCreatePhase()
    {
        var uow = CreateUnitOfWork();
        uow.PhaseRecords.Add(FoundationPhase());
        var admin = CreateService(uow, 1, Role.ADMIN);

        var list = await admin.GetPhasesByProjectAsync(1);
        var created = await admin.CreatePhaseAsync(1, ValidCreateRequest("Structure"));

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, created.StatusCode);
        Assert.Single(uow.PhaseRecords);
    }

    [Fact]
    public async Task ClosedProjectRejectsPhaseCreation()
    {
        var uow = CreateUnitOfWork();
        uow.ProjectRecords[0].Status = ProjectStatus.COMPLETED;

        var response = await CreateService(uow, 5, Role.PM)
            .CreatePhaseAsync(1, ValidCreateRequest("Foundation"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task DuplicatePhaseNameIsAllowedOnDifferentProjects()
    {
        var uow = CreateUnitOfWork();
        uow.ProjectRecords.Add(new Project
        {
            ProjectId = 2,
            ProjectName = "Other project",
            PMUserID = 5,
            Status = ProjectStatus.PLANNING,
            BaselineStart = new DateTime(2026, 9, 1),
            BaselineEnd = new DateTime(2026, 12, 31),
            StartDate = new DateTime(2026, 9, 1)
        });
        uow.PhaseRecords.Add(new Phase
        {
            PhaseId = 1,
            ProjectId = 2,
            Name = "Foundation",
            SequenceOrder = 1,
            BaselineStart = new DateTime(2026, 9, 1),
            BaselineEnd = new DateTime(2026, 9, 30)
        });

        var response = await CreateService(uow, 5, Role.PM)
            .CreatePhaseAsync(1, ValidCreateRequest("Foundation"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(2, uow.PhaseRecords.Count);
    }

    [Fact]
    public async Task WarehouseManagerCanReadWhenProjectIsInOperationalScope()
    {
        var uow = CreateUnitOfWork();
        uow.PhaseRecords.Add(FoundationPhase());
        uow.RequestRecords.Add(new MaterialRequest
        {
            RequestId = 1,
            ProjectId = 1,
            WarehouseId = 1,
            Warehouse = new Warehouse { WarehouseId = 1, ManagerId = 8, WarehouseName = "Main", Location = "Site" }
        });

        var allowed = await CreateService(uow, 8, Role.WAREHOUSE_MANAGER).GetPhasesByProjectAsync(1);
        var denied = await CreateService(uow, 9, Role.WAREHOUSE_MANAGER).GetPhasesByProjectAsync(1);

        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Fact]
    public async Task StalePhaseUpdateIsRejected()
    {
        var uow = CreateUnitOfWork();
        uow.PhaseRecords.Add(new Phase
        {
            PhaseId = 1,
            ProjectId = 1,
            Name = "Foundation",
            SequenceOrder = 1,
            BaselineStart = new DateTime(2026, 9, 1),
            BaselineEnd = new DateTime(2026, 9, 30),
            RowVersion = [1]
        });
        var service = CreateService(uow, 5, Role.PM);

        var response = await service.UpdatePhaseAsync(1, new UpdatePhaseRequest
        {
            Name = "Updated",
            SequenceOrder = 1,
            BaselineStart = new DateTime(2026, 9, 1),
            BaselineEnd = new DateTime(2026, 9, 30),
            WorkCategoryId = 1,
            RowVersion = "Ag=="
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Foundation", uow.PhaseRecords[0].Name);
    }

    private static CreatePhaseRequest ValidCreateRequest(string name) => new()
    {
        Name = name,
        SequenceOrder = 1,
        BaselineStart = new DateTime(2026, 9, 1),
        BaselineEnd = new DateTime(2026, 9, 30),
        WorkCategoryId = 1
    };

    private static Phase FoundationPhase() => new()
    {
        PhaseId = 1,
        ProjectId = 1,
        Name = "Foundation",
        SequenceOrder = 1,
        BaselineStart = new DateTime(2026, 9, 1),
        BaselineEnd = new DateTime(2026, 9, 30)
    };

    private static TestUnitOfWork CreateUnitOfWork()
    {
        var uow = new TestUnitOfWork();
        uow.WorkCategoryRecords.Add(new WorkCategory { WorkCategoryId = 1, Name = "Structural" });
        uow.ProjectRecords.Add(new Project
        {
            ProjectId = 1,
            ProjectName = "Test project",
            PMUserID = 5,
            Status = ProjectStatus.PLANNING,
            BaselineStart = new DateTime(2026, 9, 1),
            BaselineEnd = new DateTime(2026, 12, 31),
            StartDate = new DateTime(2026, 9, 1)
        });
        return uow;
    }

    private static PhaseService CreateService(TestUnitOfWork uow, int userId, Role role)
    {
        var mapper = new MapperConfiguration(configuration =>
            configuration.AddProfile<MapperConfigurationsProfile>(), NullLoggerFactory.Instance).CreateMapper();
        return new PhaseService(uow, mapper, new FakeClaimService(userId, role));
    }
}
