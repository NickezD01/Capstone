using AutoMapper;
using cpms_Application.MyMapper;
using cpms_Application.Request.ProgressReport;
using cpms_Application.Request.PurchaseOrder;
using cpms_Application.Request.Tasks;
using cpms_Application.Request.User;
using cpms_Application.Request.Warehouse;
using cpms_Application.Services;
using cpms_Domain.Models;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;

namespace cpms_Tests;

public class BusinessRuleRegressionTests
{
    [Fact]
    public async Task AdministratorCanAssignPrivilegedRoleDirectly()
    {
        var uow = new TestUnitOfWork();
        var account = new UserAccount
        {
            Id = 20,
            Email = "pm@example.com",
            IsEmailVerified = true,
            Role = Role.CUSTOMER
        };
        uow.UserAccountRecords.Add(account);

        var response = await new UserAccountService(uow, CreateMapper(), new FakeClaimService(1, Role.ADMIN))
            .UpdateUserRoleProfileAsync(account.Id, new UpdateUserRoleRequest { Role = Role.PM });

        Assert.True(response.IsSuccess, response.ErrorMessage);
        Assert.Equal(Role.PM, account.Role);
    }

    [Fact]
    public async Task ReturnCannotExceedIssuedQuantityAfterPreviousReturns()
    {
        var uow = new TestUnitOfWork();
        uow.WarehouseRecords.Add(new Warehouse { WarehouseId = 1, WarehouseName = "W", ManagerId = 10 });
        uow.VariantRecords.Add(new MaterialVariant { VariantId = 1, MaterialId = 1, VariantName = "Steel", Unit = "kg", IsActive = true });
        var issuedItem = new MaterialRequisition { ItemId = 1, RequestId = 1, VariantId = 1, IssuedQuantity = 10, Quantity = 10, ApprovedQuantity = 10 };
        var materialRequest = new MaterialRequest
        {
            RequestId = 1,
            ProjectId = 1,
            WarehouseId = 1,
            Status = MaterialRequestStatuses.Issued,
            Requisitions = new List<MaterialRequisition> { issuedItem }
        };
        issuedItem.MaterialRequest = materialRequest;
        uow.RequestRecords.Add(materialRequest);
        uow.RequisitionRecords.Add(issuedItem);
        uow.TransactionRecords.Add(new InventoryTransaction
        {
            TransactionId = 1,
            InventoryId = 1,
            WarehouseId = 1,
            VariantId = 1,
            TransactionType = InventoryTransactionTypes.Return,
            Quantity = 4,
            ReferenceId = 1,
            ReferenceType = "MATERIAL_REQUEST"
        });

        var response = await new WarehouseService(uow, null!, new FakeClaimService(10, Role.WAREHOUSE_MANAGER))
            .ReturnInventoryAsync(new InventoryReturnRequest
            {
                WarehouseId = 1,
                VariantId = 1,
                Quantity = 7,
                MaterialRequestId = 1
            });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("remaining returnable quantity of 6", response.ErrorMessage);
    }

    [Fact]
    public async Task TaskMaterialRequestUsesOnlyTheRemainingUnissuedQuantity()
    {
        var uow = new TestUnitOfWork();
        var project = new Project { ProjectId = 1, ProjectName = "P", PMUserID = 5 };
        var variant = new MaterialVariant { VariantId = 1, MaterialId = 1, VariantName = "Steel", Unit = "kg", IsActive = true };
        var task = new TaskItem { TaskId = 1, ProjectId = 1, TaskName = "T", PhaseName = "P", BaselineStart = DateTime.UtcNow };
        var requirement = new TaskMaterialRequirement { Id = 1, TaskId = 1, TaskItem = task, VariantId = 1, Variant = variant, GrossQuantityRequired = 100 };
        task.MaterialRequirements.Add(requirement);
        var oldRequest = new MaterialRequest { RequestId = 1, ProjectId = 1, TaskId = 1, Status = MaterialRequestStatuses.Issued };
        var oldItem = new MaterialRequisition { ItemId = 1, RequestId = 1, MaterialRequest = oldRequest, VariantId = 1, Quantity = 40, ApprovedQuantity = 40, IssuedQuantity = 40 };
        uow.ProjectRecords.Add(project);
        uow.TaskRecords.Add(task);
        uow.RequirementRecords.Add(requirement);
        uow.VariantRecords.Add(variant);
        uow.RequestRecords.Add(oldRequest);
        uow.RequisitionRecords.Add(oldItem);

        var response = await new MaterialRequestService(uow, CreateMapper(), new FakeClaimService(5, Role.PM))
            .CreateRequestByTaskIdAsync(1);

        Assert.True(response.IsSuccess, response.ErrorMessage);
        var newItem = Assert.Single(uow.RequisitionRecords, x => x.ItemId != 1);
        Assert.Equal(60, newItem.Quantity);
    }

    [Fact]
    public async Task TaskPlannedBudgetsCannotExceedProjectBudget()
    {
        var uow = new TestUnitOfWork();
        var start = DateTime.UtcNow.Date;
        uow.ProjectRecords.Add(new Project
        {
            ProjectId = 1,
            ProjectName = "P",
            PMUserID = 5,
            BaselineStart = start,
            BaselineEnd = start.AddMonths(2),
            TotalProjectBudget = 100
        });
        uow.TaskRecords.Add(new TaskItem { TaskId = 1, ProjectId = 1, TaskName = "Existing", PhaseName = "P", PlannedBudget = 80 });

        var response = await new TaskService(uow, null!, new FakeClaimService(5, Role.PM)).CreateTaskAsync(new CreateTaskRequest
        {
            ProjectId = 1,
            AssignedToUserID = 9,
            PhaseName = "P",
            TaskName = "New",
            PlannedBudget = 30,
            BaselineStart = start,
            BaselineEnd = start.AddDays(10)
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task OwningPmCreatesTaskAssignedToSelfAndIgnoresSuppliedAssignee()
    {
        var uow = new TestUnitOfWork();
        var start = DateTime.UtcNow.Date;
        uow.ProjectRecords.Add(new Project
        {
            ProjectId = 1,
            ProjectName = "P",
            PMUserID = 5,
            BaselineStart = start,
            BaselineEnd = start.AddMonths(2),
            TotalProjectBudget = 1000
        });
        uow.UserAccountRecords.Add(new UserAccount
        {
            Id = 9,
            Email = "worker@example.com",
            IsEmailVerified = true,
            Role = Role.WORKER
        });

        var response = await new TaskService(uow, CreateMapper(), new FakeClaimService(5, Role.PM))
            .CreateTaskAsync(new CreateTaskRequest
            {
                ProjectId = 1,
                AssignedToUserID = 9,
                PhaseName = "Foundation",
                TaskName = "Excavation",
                PlannedBudget = 100,
                BaselineStart = start,
                BaselineEnd = start.AddDays(10)
            });

        Assert.True(response.IsSuccess, response.ErrorMessage);
        var task = Assert.Single(uow.TaskRecords);
        Assert.Equal(5, task.AssignedToUserID);
        Assert.Equal(0, task.ActualCost);
        Assert.Equal(0, task.ActualProgressPct);
        Assert.Equal(cpms_Domain.Models.TaskStatus.PENDING, task.Status);
    }

    [Fact]
    public async Task NonOwningPmCannotCreateTask()
    {
        var uow = new TestUnitOfWork();
        var start = DateTime.UtcNow.Date;
        uow.ProjectRecords.Add(new Project
        {
            ProjectId = 1,
            ProjectName = "P",
            PMUserID = 5,
            BaselineStart = start,
            BaselineEnd = start.AddMonths(2)
        });

        var response = await new TaskService(uow, CreateMapper(), new FakeClaimService(6, Role.PM))
            .CreateTaskAsync(new CreateTaskRequest
            {
                ProjectId = 1,
                AssignedToUserID = 6,
                PhaseName = "Foundation",
                TaskName = "Excavation",
                PlannedBudget = 100,
                BaselineStart = start,
                BaselineEnd = start.AddDays(10)
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(uow.TaskRecords);
    }

    [Fact]
    public async Task OwningPmUpdatesTaskAndKeepsAssignmentToSelf()
    {
        var uow = new TestUnitOfWork();
        var start = DateTime.UtcNow.Date;
        var rowVersion = new byte[] { 1, 2, 3 };
        uow.ProjectRecords.Add(new Project
        {
            ProjectId = 1,
            ProjectName = "P",
            PMUserID = 5,
            BaselineStart = start,
            BaselineEnd = start.AddMonths(2),
            TotalProjectBudget = 1000
        });
        var task = new TaskItem
        {
            TaskId = 1,
            ProjectId = 1,
            AssignedToUserID = 9,
            PhaseName = "Old phase",
            TaskName = "Old task",
            PlannedBudget = 50,
            BaselineStart = start,
            BaselineEnd = start.AddDays(5),
            RowVersion = rowVersion
        };
        uow.TaskRecords.Add(task);

        var response = await new TaskService(uow, CreateMapper(), new FakeClaimService(5, Role.PM))
            .UpdateTaskAsync(1, new UpdateTaskRequest
            {
                AssignedToUserID = 11,
                PhaseName = "New phase",
                TaskName = "New task",
                PlannedBudget = 75,
                BaselineStart = start.AddDays(1),
                BaselineEnd = start.AddDays(8),
                RowVersion = Convert.ToBase64String(rowVersion)
            });

        Assert.True(response.IsSuccess, response.ErrorMessage);
        Assert.Equal(5, task.AssignedToUserID);
        Assert.Equal("New task", task.TaskName);
        Assert.Equal(75, task.PlannedBudget);
    }

    [Fact]
    public async Task TaskBaselineDatesMustRemainInsideProjectBaseline()
    {
        var uow = new TestUnitOfWork();
        var start = DateTime.UtcNow.Date;
        uow.ProjectRecords.Add(new Project
        {
            ProjectId = 1,
            ProjectName = "P",
            PMUserID = 5,
            BaselineStart = start,
            BaselineEnd = start.AddMonths(2)
        });

        var response = await new TaskService(uow, CreateMapper(), new FakeClaimService(5, Role.PM))
            .CreateTaskAsync(new CreateTaskRequest
            {
                ProjectId = 1,
                AssignedToUserID = 5,
                PhaseName = "Foundation",
                TaskName = "Excavation",
                PlannedBudget = 100,
                BaselineStart = start.AddDays(-1),
                BaselineEnd = start.AddDays(10)
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(uow.TaskRecords);
    }

    [Fact]
    public async Task PurchaseOrderRejectsDuplicateResolvedVariants()
    {
        var uow = new TestUnitOfWork();
        uow.ProjectRecords.Add(new Project { ProjectId = 1, ProjectName = "P", PMUserID = 5, TotalProjectBudget = 1000 });
        uow.WarehouseRecords.Add(new Warehouse { WarehouseId = 1, WarehouseName = "W", ManagerId = 10 });
        uow.SupplierRecords.Add(new Supplier { SupplierId = 1, CompanyName = "S" });
        uow.VariantRecords.Add(new MaterialVariant { VariantId = 1, MaterialId = 1, VariantName = "Steel", Unit = "kg", IsActive = true });
        uow.SupplierCatalogRecords.Add(new SupplierCatalog { CatalogId = 1, SupplierId = 1, VariantId = 1, UnitPrice = 5, IsAvailable = true });

        var response = await new PurchaseOrderService(uow, null!, new FakeClaimService(10, Role.WAREHOUSE_MANAGER))
            .CreatePurchaseOrderAsync(new CreatePurchaseOrderRequest
            {
                ProjectId = 1,
                WarehouseId = 1,
                SupplierId = 1,
                Items =
                {
                    new() { VariantId = 1, Quantity = 2 },
                    new() { VariantId = 1, Quantity = 3 }
                }
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("only appear once", response.ErrorMessage);
    }

    [Fact]
    public async Task ProgressUsesInProgressStatusInsteadOfLegacyActive()
    {
        var uow = new TestUnitOfWork();
        var task = new TaskItem { TaskId = 1, ProjectId = 1, TaskName = "T", PhaseName = "P", AssignedToUserID = 9 };
        var project = new Project { ProjectId = 1, ProjectName = "P", PMUserID = 5, BaselineEnd = DateTime.UtcNow.AddDays(10), Tasks = new List<TaskItem> { task } };
        uow.TaskRecords.Add(task);
        uow.ProjectRecords.Add(project);

        var response = await new ProgressReportService(uow, CreateMapper(), new FakeClaimService(5, Role.PM))
            .SubmitReportAsync(new SubmitProgressReportRequest { TaskId = 1, ProgressIncrement = 10, ActualCostIncrement = 5 });

        Assert.True(response.IsSuccess);
        var report = Assert.Single(uow.ProgressReportRecords);
        var approved = await new ProgressReportService(uow, CreateMapper(), new FakeClaimService(5, Role.PM))
            .ApproveReportAsync(report.ReportId, new ReviewProgressReportRequest { AllowCostOverrun = true });
        Assert.True(approved.IsSuccess);
        Assert.Equal(cpms_Domain.Models.TaskStatus.IN_PROGRESS, task.Status);
    }

    [Theory]
    [InlineData(Role.WORKER)]
    [InlineData(Role.CUSTOMER)]
    public async Task NonPmCannotSubmitProgressEvenWhenAssignedToTask(Role role)
    {
        var uow = new TestUnitOfWork();
        var task = new TaskItem
        {
            TaskId = 1,
            ProjectId = 1,
            TaskName = "T",
            PhaseName = "P",
            AssignedToUserID = 9
        };
        uow.TaskRecords.Add(task);
        uow.ProjectRecords.Add(new Project
        {
            ProjectId = 1,
            ProjectName = "P",
            PMUserID = 5,
            BaselineEnd = DateTime.UtcNow.AddDays(10),
            Tasks = new List<TaskItem> { task }
        });

        var response = await new ProgressReportService(uow, CreateMapper(), new FakeClaimService(9, role))
            .SubmitReportAsync(new SubmitProgressReportRequest
            {
                TaskId = 1,
                ProgressIncrement = 10,
                ActualCostIncrement = 5
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(uow.ProgressReportRecords);
    }

    private static IMapper CreateMapper() => new MapperConfiguration(configuration =>
        configuration.AddProfile<MapperConfigurationsProfile>(), NullLoggerFactory.Instance).CreateMapper();
}
