using AutoMapper;
using cpms_Application.MyMapper;
using cpms_Application.Request.MaterialRequest;
using cpms_Application.Request.ProgressReport;
using cpms_Application.Request.PurchaseOrder;
using cpms_Application.Request.Project;
using cpms_Application.Request.Supplier;
using cpms_Application.Request.SupplierCatalog;
using cpms_Application.Request.Tasks;
using cpms_Application.Request.User;
using cpms_Application.Request.Warehouse;
using cpms_Application.Response.MaterialRequest;
using cpms_Application.Response.PurchaseOrder;
using cpms_Application.Response.SupplierCatalog;
using cpms_Application.Services;
using cpms_Domain;
using cpms_Domain.Models;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;

namespace cpms_Tests;

public class BusinessRuleRegressionTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    [InlineData("  cem-42-a  ", "CEM-42-A")]
    public void InternalSkuIsCanonicalized(string? supplied, string? expected)
    {
        Assert.Equal(expected, MaterialSkuRules.Normalize(supplied));
    }

    [Fact]
    public async Task VariantReceivesGeneratedSkuAndOperationalSkuCannotBeReidentified()
    {
        var uow = new TestUnitOfWork();
        var material = new Material
        {
            MaterialId = 1,
            MaterialName = "Portland cement",
            DefaultUnit = "bag",
            IsActive = true
        };
        uow.MaterialRecords.Add(material);
        var service = new MaterialService(uow, CreateMapper());

        var created = await service.CreateVariantAsync(new cpms_Application.Request.Material.MaterialVariantRequest
        {
            MaterialId = material.MaterialId,
            VariantName = "Type I - 50 kg",
            Unit = "bag",
            IsActive = true
        });

        Assert.True(created.IsSuccess, created.ErrorMessage);
        var variant = Assert.Single(uow.VariantRecords);
        Assert.Equal("MAT-000001-VAR-000001", variant.SKU);

        uow.InventoryRecords.Add(new InventoryRecord
        {
            InventoryId = 1,
            WarehouseId = 1,
            VariantId = variant.VariantId,
            QuantityOnHand = 10
        });
        var reidentified = await service.UpdateVariantAsync(variant.VariantId,
            new cpms_Application.Request.Material.MaterialVariantRequest
            {
                MaterialId = material.MaterialId,
                VariantName = variant.VariantName,
                SKU = "A-DIFFERENT-SKU",
                Unit = variant.Unit,
                IsActive = true
            });

        Assert.Equal(HttpStatusCode.Conflict, reidentified.StatusCode);
        Assert.Equal("MAT-000001-VAR-000001", variant.SKU);
    }

    [Fact]
    public async Task VariantCannotBeDeactivatedWhileWarehouseBalanceRemains()
    {
        var uow = new TestUnitOfWork();
        var material = new Material { MaterialId = 1, MaterialName = "Cement", DefaultUnit = "bag", IsActive = true };
        var variant = new MaterialVariant
        {
            VariantId = 1,
            MaterialId = 1,
            Material = material,
            VariantName = "Type I",
            Unit = "bag",
            SKU = "CEM-T1",
            IsActive = true
        };
        material.Variants.Add(variant);
        uow.MaterialRecords.Add(material);
        uow.VariantRecords.Add(variant);
        uow.InventoryRecords.Add(new InventoryRecord
        {
            InventoryId = 1,
            WarehouseId = 1,
            VariantId = 1,
            QuantityOnHand = 5
        });

        var response = await new MaterialService(uow, CreateMapper()).DeleteVariantAsync(variant.VariantId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.True(variant.IsActive);
        Assert.False(variant.IsDeleted);
    }

    [Fact]
    public async Task VariantDeactivationPreservesHistoryAndDisablesSupplierOffers()
    {
        var uow = new TestUnitOfWork();
        var material = new Material { MaterialId = 1, MaterialName = "Cement", DefaultUnit = "bag", IsActive = true };
        var variant = new MaterialVariant
        {
            VariantId = 1,
            MaterialId = 1,
            Material = material,
            VariantName = "Type I",
            Unit = "bag",
            SKU = "CEM-T1",
            IsActive = true
        };
        var deliveredOrder = new PurchaseOrder { PoId = 1, Status = PurchaseOrderStatus.DELIVERED };
        uow.MaterialRecords.Add(material);
        uow.VariantRecords.Add(variant);
        uow.OrderLineRecords.Add(new OrderLineItem
        {
            LineItemId = 1,
            PoId = 1,
            PurchaseOrder = deliveredOrder,
            VariantId = 1,
            Variant = variant,
            Quantity = 2
        });
        var catalog = new SupplierCatalog
        {
            CatalogId = 1,
            SupplierId = 1,
            VariantId = 1,
            IsAvailable = true
        };
        uow.SupplierCatalogRecords.Add(catalog);

        var response = await new MaterialService(uow, CreateMapper()).DeleteVariantAsync(variant.VariantId);

        Assert.True(response.IsSuccess, response.ErrorMessage);
        Assert.False(variant.IsActive);
        Assert.False(variant.IsDeleted);
        Assert.False(catalog.IsAvailable);
        Assert.Same(variant, Assert.Single(uow.VariantRecords));
    }

    [Fact]
    public async Task DeactivatingMaterialSafelyCascadesToItsVariantsAndCatalogs()
    {
        var uow = new TestUnitOfWork();
        var material = new Material { MaterialId = 1, MaterialName = "Cement", DefaultUnit = "bag", IsActive = true };
        var variant = new MaterialVariant
        {
            VariantId = 1,
            MaterialId = 1,
            Material = material,
            VariantName = "Type I",
            Unit = "bag",
            SKU = "CEM-T1",
            IsActive = true
        };
        material.Variants.Add(variant);
        var catalog = new SupplierCatalog { CatalogId = 1, SupplierId = 1, VariantId = 1, IsAvailable = true };
        uow.MaterialRecords.Add(material);
        uow.VariantRecords.Add(variant);
        uow.SupplierCatalogRecords.Add(catalog);

        var response = await new MaterialService(uow, CreateMapper()).UpdateMaterialAsync(material.MaterialId,
            new cpms_Application.Request.Material.UpdateMaterialRequest
            {
                MaterialName = material.MaterialName,
                DefaultUnit = material.DefaultUnit,
                IsActive = false
            });

        Assert.True(response.IsSuccess, response.ErrorMessage);
        Assert.False(material.IsActive);
        Assert.False(material.IsDeleted);
        Assert.False(variant.IsActive);
        Assert.False(variant.IsDeleted);
        Assert.False(catalog.IsAvailable);
    }

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
    public async Task PartiallyApprovedRequestBlocksAnOverlappingTaskRequest()
    {
        var uow = new TestUnitOfWork();
        uow.ProjectRecords.Add(new Project { ProjectId = 1, ProjectName = "P", PMUserID = 5, Status = ProjectStatus.IN_PROGRESS });
        uow.TaskRecords.Add(new TaskItem { TaskId = 1, ProjectId = 1, TaskName = "T", PhaseName = "P" });
        uow.VariantRecords.Add(new MaterialVariant { VariantId = 1, MaterialId = 1, VariantName = "Steel", Unit = "kg", IsActive = true });
        uow.RequestRecords.Add(new MaterialRequest
        {
            RequestId = 1,
            ProjectId = 1,
            TaskId = 1,
            Status = MaterialRequestStatuses.PartiallyApproved
        });

        var response = await new MaterialRequestService(uow, CreateMapper(), new FakeClaimService(5, Role.PM))
            .CreateRequestAsync(new CreateMaterialRequest
            {
                ProjectId = 1,
                TaskId = 1,
                Items = { new() { VariantId = 1, Quantity = 10 } }
            });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("already has", response.ErrorMessage);
    }

    [Fact]
    public async Task PartiallyIssuedRequestKeepsItsShortageInTheOriginalFulfilmentFlow()
    {
        var uow = new TestUnitOfWork();
        uow.ProjectRecords.Add(new Project { ProjectId = 1, ProjectName = "P", PMUserID = 5, Status = ProjectStatus.IN_PROGRESS });
        uow.TaskRecords.Add(new TaskItem { TaskId = 1, ProjectId = 1, TaskName = "T", PhaseName = "P" });
        uow.VariantRecords.Add(new MaterialVariant { VariantId = 1, MaterialId = 1, VariantName = "Steel", Unit = "kg", IsActive = true });
        uow.RequirementRecords.Add(new TaskMaterialRequirement { Id = 1, TaskId = 1, VariantId = 1, GrossQuantityRequired = 10 });
        uow.RequestRecords.Add(new MaterialRequest
        {
            RequestId = 1,
            ProjectId = 1,
            TaskId = 1,
            Status = MaterialRequestStatuses.PartiallyIssued
        });

        var response = await new MaterialRequestService(uow, CreateMapper(), new FakeClaimService(5, Role.PM))
            .CreateRequestAsync(new CreateMaterialRequest
            {
                ProjectId = 1,
                TaskId = 1,
                Items = { new() { VariantId = 1, Quantity = 5 } }
            });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("already has", response.ErrorMessage);
    }

    [Fact]
    public async Task AssignedRequestCannotBeApprovedIntoAnotherWarehouse()
    {
        var uow = new TestUnitOfWork();
        uow.WarehouseRecords.AddRange(new[]
        {
            new Warehouse { WarehouseId = 1, WarehouseName = "Assigned", ManagerId = 10 },
            new Warehouse { WarehouseId = 2, WarehouseName = "Other", ManagerId = 20 }
        });
        uow.RequestRecords.Add(new MaterialRequest
        {
            RequestId = 1,
            ProjectId = 1,
            WarehouseId = 1,
            Status = MaterialRequestStatuses.Pending,
            Requisitions = new List<MaterialRequisition>
            {
                new() { ItemId = 1, RequestId = 1, VariantId = 1, Quantity = 5 }
            }
        });

        var response = await new MaterialRequestService(uow, CreateMapper(), new FakeClaimService(20, Role.WAREHOUSE_MANAGER))
            .ApproveRequestAsync(1, new ApproveMaterialRequest
            {
                WarehouseId = 2,
                Items = { new() { ItemId = 1, ApprovedQuantity = 5 } }
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(uow.ReservationRecords);
    }

    [Fact]
    public async Task MaterialIssueRecordsTheWeightedAverageCost()
    {
        var uow = new TestUnitOfWork();
        var material = new Material { MaterialId = 1, MaterialName = "Steel", DefaultUnit = "kg" };
        var variant = new MaterialVariant { VariantId = 1, MaterialId = 1, Material = material, VariantName = "Grade 60", Unit = "kg", IsActive = true };
        var warehouse = new Warehouse { WarehouseId = 1, WarehouseName = "Main", ManagerId = 10 };
        var project = new Project { ProjectId = 1, ProjectName = "P", PMUserID = 5 };
        var inventory = new InventoryRecord
        {
            InventoryId = 1,
            WarehouseId = 1,
            Warehouse = warehouse,
            VariantId = 1,
            Variant = variant,
            QuantityOnHand = 10,
            ReservedQuantity = 5,
            AverageUnitCost = 12.5m
        };
        var item = new MaterialRequisition
        {
            ItemId = 1,
            RequestId = 1,
            VariantId = 1,
            Variant = variant,
            Quantity = 5,
            ApprovedQuantity = 5
        };
        var reservation = new InventoryReservation
        {
            ReservationId = 1,
            InventoryId = 1,
            InventoryRecord = inventory,
            RequestId = 1,
            RequestItemId = 1,
            Quantity = 5,
            Status = InventoryReservationStatuses.Active
        };
        var request = new MaterialRequest
        {
            RequestId = 1,
            ProjectId = 1,
            Project = project,
            WarehouseId = 1,
            Warehouse = warehouse,
            Status = MaterialRequestStatuses.Approved,
            Requisitions = new List<MaterialRequisition> { item },
            Reservations = new List<InventoryReservation> { reservation }
        };
        item.MaterialRequest = request;
        reservation.MaterialRequest = request;
        reservation.RequestItem = item;
        uow.ProjectRecords.Add(project);
        uow.WarehouseRecords.Add(warehouse);
        uow.VariantRecords.Add(variant);
        uow.InventoryRecords.Add(inventory);
        uow.RequisitionRecords.Add(item);
        uow.ReservationRecords.Add(reservation);
        uow.RequestRecords.Add(request);

        var response = await new MaterialRequestService(uow, CreateMapper(), new FakeClaimService(10, Role.WAREHOUSE_MANAGER))
            .IssueRequestAsync(1);

        Assert.True(response.IsSuccess, response.ErrorMessage);
        var transaction = Assert.Single(uow.TransactionRecords);
        Assert.Equal(12.5m, transaction.UnitCost);
        Assert.Equal(62.5m, transaction.TotalValue);
    }

    [Fact]
    public async Task PartiallyIssuedRequestCanReturnUnusedMaterial()
    {
        var uow = new TestUnitOfWork();
        var material = new Material { MaterialId = 1, MaterialName = "Steel", DefaultUnit = "kg" };
        var variant = new MaterialVariant { VariantId = 1, MaterialId = 1, Material = material, VariantName = "Grade 60", Unit = "kg", IsActive = true };
        var warehouse = new Warehouse { WarehouseId = 1, WarehouseName = "Main", ManagerId = 10 };
        var inventory = new InventoryRecord
        {
            InventoryId = 1,
            WarehouseId = 1,
            Warehouse = warehouse,
            VariantId = 1,
            Variant = variant,
            QuantityOnHand = 5,
            AverageUnitCost = 12.5m,
            RowVersion = Array.Empty<byte>()
        };
        var item = new MaterialRequisition { ItemId = 1, RequestId = 1, VariantId = 1, Quantity = 10, ApprovedQuantity = 5, IssuedQuantity = 5 };
        var request = new MaterialRequest
        {
            RequestId = 1,
            ProjectId = 1,
            WarehouseId = 1,
            Status = MaterialRequestStatuses.PartiallyIssued,
            Requisitions = new List<MaterialRequisition> { item }
        };
        item.MaterialRequest = request;
        uow.WarehouseRecords.Add(warehouse);
        uow.VariantRecords.Add(variant);
        uow.InventoryRecords.Add(inventory);
        uow.RequestRecords.Add(request);
        uow.RequisitionRecords.Add(item);

        var response = await new WarehouseService(uow, CreateMapper(), new FakeClaimService(10, Role.WAREHOUSE_MANAGER))
            .ReturnInventoryAsync(new InventoryReturnRequest
            {
                WarehouseId = 1,
                VariantId = 1,
                MaterialRequestId = 1,
                Quantity = 2
            });

        Assert.True(response.IsSuccess, response.ErrorMessage);
        Assert.Equal(7, inventory.QuantityOnHand);
        Assert.Single(uow.MaterialReturnRecords);
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
    public async Task PurchaseOrderRequiresVariantIdWhenMaterialHasMultipleVariants()
    {
        var uow = new TestUnitOfWork();
        uow.ProjectRecords.Add(new Project { ProjectId = 1, ProjectName = "P", PMUserID = 5, TotalProjectBudget = 1000 });
        uow.WarehouseRecords.Add(new Warehouse { WarehouseId = 1, WarehouseName = "W", ManagerId = 10 });
        uow.SupplierRecords.Add(new Supplier { SupplierId = 1, CompanyName = "S" });
        uow.VariantRecords.AddRange(new[]
        {
            new MaterialVariant { VariantId = 1, MaterialId = 1, VariantName = "Steel 10 mm", Unit = "m", IsActive = true },
            new MaterialVariant { VariantId = 2, MaterialId = 1, VariantName = "Steel 12 mm", Unit = "m", IsActive = true }
        });

        var response = await new PurchaseOrderService(uow, null!, new FakeClaimService(10, Role.WAREHOUSE_MANAGER))
            .CreatePurchaseOrderAsync(new CreatePurchaseOrderRequest
            {
                ProjectId = 1,
                WarehouseId = 1,
                SupplierId = 1,
                Items = { new() { MaterialId = 1, Quantity = 2 } }
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("exactly one active variant", response.ErrorMessage);
    }

    [Fact]
    public async Task CatalogOffersExposeTheRulesNeededToCreatePurchaseOrders()
    {
        var uow = new TestUnitOfWork();
        var material = new Material { MaterialId = 1, MaterialName = "Portland cement", DefaultUnit = "bag" };
        var variant = new MaterialVariant
        {
            VariantId = 2,
            MaterialId = 1,
            Material = material,
            VariantName = "Type I - 50 kg",
            SKU = "CEM-T1-50",
            Unit = "bag",
            IsActive = true
        };
        var supplier = new Supplier { SupplierId = 3, CompanyName = "Reliable Supply" };
        uow.SupplierCatalogRecords.Add(new SupplierCatalog
        {
            CatalogId = 4,
            SupplierId = supplier.SupplierId,
            Supplier = supplier,
            VariantId = variant.VariantId,
            Variant = variant,
            SupplierSku = "RS-CEM-50",
            UnitPrice = 125000,
            MinimumOrderQuantity = 20,
            LeadTimeDays = 3,
            IsAvailable = true
        });

        var response = await new CatalogService(uow, null!)
            .GetCatalogOffersAsync(null, variant.VariantId);

        Assert.True(response.IsSuccess, response.ErrorMessage);
        var offer = Assert.Single(Assert.IsType<List<CatalogOfferResponse>>(response.Result));
        Assert.Equal("Reliable Supply", offer.SupplierName);
        Assert.Equal("CEM-T1-50", offer.Sku);
        Assert.Equal(20, offer.MinimumOrderQuantity);
        Assert.Equal(3, offer.LeadTimeDays);
    }

    [Fact]
    public async Task ShortagePurchaseOrderAllowsOnlySupplierMinimumExcessAndDefaultsDeliveryDate()
    {
        var uow = new TestUnitOfWork();
        var material = new Material { MaterialId = 1, MaterialName = "Steel", DefaultUnit = "kg" };
        var variant = new MaterialVariant
        {
            VariantId = 1,
            MaterialId = 1,
            Material = material,
            VariantName = "Grade 60",
            Unit = "kg",
            IsActive = true
        };
        var project = new Project { ProjectId = 1, ProjectName = "P", PMUserID = 5, TotalProjectBudget = 1000 };
        var warehouse = new Warehouse { WarehouseId = 1, WarehouseName = "W", ManagerId = 10 };
        var supplier = new Supplier { SupplierId = 1, CompanyName = "S" };
        var materialRequest = new MaterialRequest
        {
            RequestId = 1,
            ProjectId = 1,
            TaskId = 7,
            WarehouseId = 1,
            Status = MaterialRequestStatuses.PartiallyApproved
        };
        var requestItem = new MaterialRequisition
        {
            ItemId = 1,
            RequestId = 1,
            VariantId = 1,
            Variant = variant,
            MaterialRequest = materialRequest,
            Quantity = 9,
            ApprovedQuantity = 5
        };
        uow.ProjectRecords.Add(project);
        uow.WarehouseRecords.Add(warehouse);
        uow.SupplierRecords.Add(supplier);
        uow.VariantRecords.Add(variant);
        uow.RequestRecords.Add(materialRequest);
        uow.RequisitionRecords.Add(requestItem);
        uow.SupplierCatalogRecords.Add(new SupplierCatalog
        {
            CatalogId = 1,
            SupplierId = 1,
            Supplier = supplier,
            VariantId = 1,
            Variant = variant,
            UnitPrice = 5,
            MinimumOrderQuantity = 10,
            LeadTimeDays = 4,
            IsAvailable = true
        });

        var response = await new PurchaseOrderService(uow, CreateMapper(), new FakeClaimService(10, Role.WAREHOUSE_MANAGER))
            .CreatePurchaseOrderAsync(new CreatePurchaseOrderRequest
            {
                ProjectId = 1,
                WarehouseId = 1,
                SupplierId = 1,
                Items = { new() { VariantId = 1, RequestItemId = 1, Quantity = 10 } }
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = Assert.Single(uow.PurchaseOrderRecords);
        Assert.Equal(10, Assert.Single(order.OrderLineItems).Quantity);
        Assert.Equal(DateTime.UtcNow.Date.AddDays(4), order.ExpectedDeliveryDate);
    }

    [Fact]
    public async Task ShortagePurchaseOrderRejectsADifferentDestinationWarehouse()
    {
        var uow = new TestUnitOfWork();
        var variant = new MaterialVariant { VariantId = 1, MaterialId = 1, VariantName = "Steel", Unit = "kg", IsActive = true };
        var materialRequest = new MaterialRequest
        {
            RequestId = 1,
            ProjectId = 1,
            TaskId = 7,
            WarehouseId = 1,
            Status = MaterialRequestStatuses.PartiallyApproved
        };
        var requestItem = new MaterialRequisition
        {
            ItemId = 1,
            RequestId = 1,
            VariantId = 1,
            MaterialRequest = materialRequest,
            Quantity = 5
        };
        uow.ProjectRecords.Add(new Project { ProjectId = 1, ProjectName = "P", TotalProjectBudget = 1000 });
        uow.WarehouseRecords.Add(new Warehouse { WarehouseId = 2, WarehouseName = "Wrong", ManagerId = 10 });
        uow.SupplierRecords.Add(new Supplier { SupplierId = 1, CompanyName = "S" });
        uow.VariantRecords.Add(variant);
        uow.RequestRecords.Add(materialRequest);
        uow.RequisitionRecords.Add(requestItem);
        uow.SupplierCatalogRecords.Add(new SupplierCatalog
        {
            CatalogId = 1,
            SupplierId = 1,
            VariantId = 1,
            UnitPrice = 5,
            MinimumOrderQuantity = 1,
            IsAvailable = true
        });

        var response = await new PurchaseOrderService(uow, null!, new FakeClaimService(10, Role.WAREHOUSE_MANAGER))
            .CreatePurchaseOrderAsync(new CreatePurchaseOrderRequest
            {
                ProjectId = 1,
                WarehouseId = 2,
                SupplierId = 1,
                Items = { new() { VariantId = 1, RequestItemId = 1, Quantity = 5 } }
            });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("warehouse assigned", response.ErrorMessage);
    }

    [Fact]
    public async Task DamagedPriorDeliveryReopensOnlyTheUncoveredShortage()
    {
        var uow = new TestUnitOfWork();
        var material = new Material { MaterialId = 1, MaterialName = "Steel", DefaultUnit = "kg" };
        var variant = new MaterialVariant
        {
            VariantId = 1,
            MaterialId = 1,
            Material = material,
            VariantName = "Grade 60",
            Unit = "kg",
            IsActive = true
        };
        var project = new Project { ProjectId = 1, ProjectName = "P", TotalProjectBudget = 1000 };
        var warehouse = new Warehouse { WarehouseId = 1, WarehouseName = "W", ManagerId = 10 };
        var supplier = new Supplier { SupplierId = 1, CompanyName = "S" };
        var materialRequest = new MaterialRequest
        {
            RequestId = 1,
            ProjectId = 1,
            TaskId = 7,
            WarehouseId = 1,
            Status = MaterialRequestStatuses.PartiallyApproved
        };
        var requestItem = new MaterialRequisition
        {
            ItemId = 1,
            RequestId = 1,
            VariantId = 1,
            Variant = variant,
            MaterialRequest = materialRequest,
            Quantity = 10,
            ApprovedQuantity = 7
        };
        var priorOrder = new PurchaseOrder
        {
            PoId = 1,
            ProjectId = 1,
            WarehouseId = 1,
            SupplierId = 1,
            Status = PurchaseOrderStatus.CLOSED_WITH_VARIANCE
        };
        var priorLine = new OrderLineItem
        {
            LineItemId = 1,
            PoId = 1,
            PurchaseOrder = priorOrder,
            VariantId = 1,
            RequestItemId = 1,
            RequestItem = requestItem,
            Quantity = 10,
            ReceivedQuantity = 7,
            DamagedQuantity = 3
        };
        priorOrder.OrderLineItems.Add(priorLine);
        uow.ProjectRecords.Add(project);
        uow.WarehouseRecords.Add(warehouse);
        uow.SupplierRecords.Add(supplier);
        uow.VariantRecords.Add(variant);
        uow.RequestRecords.Add(materialRequest);
        uow.RequisitionRecords.Add(requestItem);
        uow.PurchaseOrderRecords.Add(priorOrder);
        uow.OrderLineRecords.Add(priorLine);
        uow.SupplierCatalogRecords.Add(new SupplierCatalog
        {
            CatalogId = 1,
            SupplierId = 1,
            Supplier = supplier,
            VariantId = 1,
            Variant = variant,
            UnitPrice = 5,
            MinimumOrderQuantity = 1,
            LeadTimeDays = 2,
            IsAvailable = true
        });

        var response = await new PurchaseOrderService(uow, CreateMapper(), new FakeClaimService(10, Role.WAREHOUSE_MANAGER))
            .CreatePurchaseOrderAsync(new CreatePurchaseOrderRequest
            {
                ProjectId = 1,
                WarehouseId = 1,
                SupplierId = 1,
                Items = { new() { VariantId = 1, RequestItemId = 1, Quantity = 3 } }
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var replacement = Assert.Single(uow.PurchaseOrderRecords, order => order.PoId != priorOrder.PoId);
        Assert.Equal(3, Assert.Single(replacement.OrderLineItems).Quantity);
    }

    [Fact]
    public async Task ProcurementShortageSuggestionsUseExistingCoverageAndSupplierMoq()
    {
        var uow = new TestUnitOfWork();
        var project = new Project { ProjectId = 1, ProjectName = "Tower A" };
        var warehouse = new Warehouse { WarehouseId = 1, WarehouseName = "Main site store", ManagerId = 10 };
        var material = new Material { MaterialId = 1, MaterialName = "Portland cement", DefaultUnit = "bag" };
        var variant = new MaterialVariant
        {
            VariantId = 1,
            MaterialId = 1,
            Material = material,
            VariantName = "Type I - 50 kg",
            SKU = "CEM-T1-50",
            Unit = "bag",
            IsActive = true
        };
        var materialRequest = new MaterialRequest
        {
            RequestId = 1,
            ProjectId = 1,
            Project = project,
            TaskId = 5,
            WarehouseId = 1,
            Warehouse = warehouse,
            Status = MaterialRequestStatuses.PartiallyApproved
        };
        var requestItem = new MaterialRequisition
        {
            ItemId = 1,
            RequestId = 1,
            MaterialRequest = materialRequest,
            VariantId = 1,
            Variant = variant,
            Quantity = 10,
            ApprovedQuantity = 4,
            NeededByDate = DateTime.UtcNow.Date.AddDays(7)
        };
        var priorOrder = new PurchaseOrder { PoId = 1, Status = PurchaseOrderStatus.PENDING };
        var priorLine = new OrderLineItem
        {
            LineItemId = 1,
            PoId = 1,
            PurchaseOrder = priorOrder,
            VariantId = 1,
            RequestItemId = 1,
            RequestItem = requestItem,
            Quantity = 2
        };
        var supplier = new Supplier { SupplierId = 1, CompanyName = "Cement Supply Co." };
        uow.ProjectRecords.Add(project);
        uow.WarehouseRecords.Add(warehouse);
        uow.VariantRecords.Add(variant);
        uow.RequestRecords.Add(materialRequest);
        uow.RequisitionRecords.Add(requestItem);
        uow.PurchaseOrderRecords.Add(priorOrder);
        uow.OrderLineRecords.Add(priorLine);
        uow.SupplierRecords.Add(supplier);
        uow.SupplierCatalogRecords.Add(new SupplierCatalog
        {
            CatalogId = 1,
            SupplierId = 1,
            Supplier = supplier,
            VariantId = 1,
            Variant = variant,
            UnitPrice = 5,
            MinimumOrderQuantity = 10,
            LeadTimeDays = 3,
            IsAvailable = true
        });

        var response = await new PurchaseOrderService(uow, null!, new FakeClaimService(10, Role.WAREHOUSE_MANAGER))
            .GetProcurementShortagesAsync();

        Assert.True(response.IsSuccess, response.ErrorMessage);
        var shortage = Assert.Single(Assert.IsType<List<ProcurementShortageResponse>>(response.Result));
        Assert.Equal(6, shortage.GrossShortageQuantity);
        Assert.Equal(2, shortage.ProcurementCoverageQuantity);
        Assert.Equal(4, shortage.RemainingShortageQuantity);
        Assert.Equal("CEM-T1-50", shortage.Sku);
        var offer = Assert.Single(shortage.SupplierOffers);
        Assert.Equal(10, offer.SuggestedOrderQuantity);
        Assert.Equal(6, offer.ExpectedExcessStockQuantity);
        Assert.Equal(50, offer.SuggestedOrderTotal);
    }

    [Fact]
    public async Task PurchaseOrderLifecycleReachesEveryOperationalStatusAndClosesFinalReceipt()
    {
        var uow = new TestUnitOfWork();
        var mapper = CreateMapper();
        var project = new Project { ProjectId = 1, ProjectName = "Tower A", PMUserID = 5 };
        var warehouse = new Warehouse { WarehouseId = 1, WarehouseName = "Main site store", ManagerId = 10 };
        var supplier = new Supplier { SupplierId = 1, CompanyName = "Steel Supply Co." };
        var material = new Material { MaterialId = 1, MaterialName = "Rebar", DefaultUnit = "kg" };
        var variant = new MaterialVariant
        {
            VariantId = 1,
            MaterialId = 1,
            Material = material,
            VariantName = "Grade 60 - 16 mm",
            SKU = "REB-G60-16",
            Grade = "60",
            Size = "16 mm",
            Specification = "Deformed reinforcing bar",
            Unit = "kg",
            IsActive = true
        };
        var line = new OrderLineItem
        {
            LineItemId = 1,
            VariantId = 1,
            Variant = variant,
            Quantity = 10,
            UnitPrice = 5
        };
        var order = new PurchaseOrder
        {
            PoId = 1,
            ProjectId = 1,
            Project = project,
            SupplierId = 1,
            Supplier = supplier,
            WarehouseId = 1,
            Warehouse = warehouse,
            UserAccountId = 10,
            Status = PurchaseOrderStatus.PENDING,
            TotalAmount = 50,
            ExpectedDeliveryDate = DateTime.UtcNow.Date.AddDays(2),
            OrderLineItems = new List<OrderLineItem> { line }
        };
        line.PoId = order.PoId;
        line.PurchaseOrder = order;
        uow.ProjectRecords.Add(project);
        uow.WarehouseRecords.Add(warehouse);
        uow.SupplierRecords.Add(supplier);
        uow.VariantRecords.Add(variant);
        uow.PurchaseOrderRecords.Add(order);
        uow.OrderLineRecords.Add(line);

        var approval = await new PurchaseOrderService(uow, mapper, new FakeClaimService(5, Role.PM))
            .ApprovePurchaseOrderAsync(order.PoId);
        Assert.True(approval.IsSuccess, approval.ErrorMessage);
        Assert.Equal(PurchaseOrderStatus.APPROVED, order.Status);
        var inventory = Assert.Single(uow.InventoryRecords);
        inventory.Warehouse = warehouse;
        inventory.Variant = variant;
        Assert.Equal(10, inventory.OnOrderQuantity);

        var processing = await new PurchaseOrderService(uow, mapper, new FakeClaimService(10, Role.WAREHOUSE_MANAGER))
            .MarkProcessingAsync(order.PoId);
        Assert.True(processing.IsSuccess, processing.ErrorMessage);
        Assert.Equal(PurchaseOrderStatus.PROCESSING, order.Status);

        var shipped = await new PurchaseOrderService(uow, mapper, new FakeClaimService(10, Role.WAREHOUSE_MANAGER))
            .MarkShippedAsync(order.PoId);
        Assert.True(shipped.IsSuccess, shipped.ErrorMessage);
        Assert.Equal(PurchaseOrderStatus.SHIPPED, order.Status);

        var invalidCancellation = await new PurchaseOrderService(uow, mapper, new FakeClaimService(5, Role.PM))
            .CancelPurchaseOrderAsync(order.PoId);
        Assert.Equal(HttpStatusCode.Conflict, invalidCancellation.StatusCode);
        Assert.Equal(PurchaseOrderStatus.SHIPPED, order.Status);

        var partialReceipt = await new PurchaseOrderService(uow, mapper, new FakeClaimService(10, Role.WAREHOUSE_MANAGER))
            .ReceivePurchaseOrderAsync(order.PoId, new ReceivePurchaseOrderRequest
            {
                Items = { new() { LineItemId = line.LineItemId, Quantity = 4 } }
            });
        Assert.True(partialReceipt.IsSuccess, partialReceipt.ErrorMessage);
        Assert.Equal(PurchaseOrderStatus.PARTIALLY_RECEIVED, order.Status);
        Assert.Equal(4, inventory.QuantityOnHand);
        Assert.Equal(6, inventory.OnOrderQuantity);

        var incompleteFinal = await new PurchaseOrderService(uow, mapper, new FakeClaimService(10, Role.WAREHOUSE_MANAGER))
            .ReceivePurchaseOrderAsync(order.PoId, new ReceivePurchaseOrderRequest
            {
                IsFinalDelivery = true,
                Items = { new() { LineItemId = line.LineItemId, Quantity = 5 } }
            });
        Assert.Equal(HttpStatusCode.BadRequest, incompleteFinal.StatusCode);
        Assert.Equal(PurchaseOrderStatus.PARTIALLY_RECEIVED, order.Status);
        Assert.Equal(4, inventory.QuantityOnHand);

        var finalReceipt = await new PurchaseOrderService(uow, mapper, new FakeClaimService(10, Role.WAREHOUSE_MANAGER))
            .ReceivePurchaseOrderAsync(order.PoId, new ReceivePurchaseOrderRequest
            {
                IsFinalDelivery = true,
                Items = { new() { LineItemId = line.LineItemId, Quantity = 5, MissingQuantity = 1 } }
            });
        Assert.True(finalReceipt.IsSuccess, finalReceipt.ErrorMessage);
        Assert.Equal(PurchaseOrderStatus.CLOSED_WITH_VARIANCE, order.Status);
        Assert.Equal(9, inventory.QuantityOnHand);
        Assert.Equal(0, inventory.OnOrderQuantity);
        Assert.Equal(2, uow.TransactionRecords.Count);
        Assert.Single(uow.SupplierMetricRecords);

        var response = Assert.IsType<PurchaseOrderResponse>(finalReceipt.Result);
        var responseLine = Assert.Single(response.Items);
        Assert.Equal("REB-G60-16", responseLine.SKU);
        Assert.Equal("Deformed reinforcing bar", responseLine.Specification);
        var inventoryResponse = mapper.Map<cpms_Application.Response.Inventory.InventoryReportResponse>(inventory);
        Assert.Equal("REB-G60-16", inventoryResponse.SKU);
    }

    [Fact]
    public async Task ShortageLinkedReceiptReservesAgainstOriginalRequestAndLeavesMissingQuantityOpen()
    {
        var uow = new TestUnitOfWork();
        var mapper = CreateMapper();
        var project = new Project { ProjectId = 1, ProjectName = "Tower A", PMUserID = 5 };
        var warehouse = new Warehouse { WarehouseId = 1, WarehouseName = "Main site store", ManagerId = 10 };
        var supplier = new Supplier { SupplierId = 1, CompanyName = "Cement Supply Co." };
        var material = new Material { MaterialId = 1, MaterialName = "Portland cement", DefaultUnit = "bag" };
        var variant = new MaterialVariant
        {
            VariantId = 1,
            MaterialId = 1,
            Material = material,
            VariantName = "Type I - 50 kg",
            SKU = "CEM-T1-50",
            Unit = "bag",
            IsActive = true
        };
        var materialRequest = new MaterialRequest
        {
            RequestId = 1,
            ProjectId = 1,
            Project = project,
            TaskId = 7,
            WarehouseId = 1,
            Warehouse = warehouse,
            Status = MaterialRequestStatuses.PartiallyApproved
        };
        var requestItem = new MaterialRequisition
        {
            ItemId = 1,
            RequestId = 1,
            MaterialRequest = materialRequest,
            VariantId = 1,
            Variant = variant,
            Quantity = 10,
            ApprovedQuantity = 4,
            NeededByDate = DateTime.UtcNow.Date.AddDays(7)
        };
        materialRequest.Requisitions.Add(requestItem);
        var inventory = new InventoryRecord
        {
            InventoryId = 1,
            WarehouseId = 1,
            Warehouse = warehouse,
            VariantId = 1,
            Variant = variant,
            QuantityOnHand = 4,
            ReservedQuantity = 4,
            OnOrderQuantity = 10,
            AverageUnitCost = 2
        };
        var existingReservation = new InventoryReservation
        {
            ReservationId = 1,
            InventoryId = 1,
            InventoryRecord = inventory,
            RequestId = 1,
            MaterialRequest = materialRequest,
            RequestItemId = 1,
            RequestItem = requestItem,
            Quantity = 4,
            Status = InventoryReservationStatuses.Active,
            ReservedAt = DateTime.UtcNow
        };
        inventory.Reservations.Add(existingReservation);
        materialRequest.Reservations.Add(existingReservation);
        requestItem.Reservations.Add(existingReservation);
        var line = new OrderLineItem
        {
            LineItemId = 1,
            VariantId = 1,
            Variant = variant,
            RequestItemId = 1,
            RequestItem = requestItem,
            Quantity = 10,
            UnitPrice = 5
        };
        var order = new PurchaseOrder
        {
            PoId = 1,
            ProjectId = 1,
            Project = project,
            SupplierId = 1,
            Supplier = supplier,
            WarehouseId = 1,
            Warehouse = warehouse,
            UserAccountId = 10,
            Status = PurchaseOrderStatus.SHIPPED,
            TotalAmount = 50,
            OrderLineItems = new List<OrderLineItem> { line }
        };
        line.PoId = order.PoId;
        line.PurchaseOrder = order;
        uow.ProjectRecords.Add(project);
        uow.WarehouseRecords.Add(warehouse);
        uow.SupplierRecords.Add(supplier);
        uow.VariantRecords.Add(variant);
        uow.RequestRecords.Add(materialRequest);
        uow.RequisitionRecords.Add(requestItem);
        uow.InventoryRecords.Add(inventory);
        uow.ReservationRecords.Add(existingReservation);
        uow.PurchaseOrderRecords.Add(order);
        uow.OrderLineRecords.Add(line);
        uow.SupplierCatalogRecords.Add(new SupplierCatalog
        {
            CatalogId = 1,
            SupplierId = 1,
            Supplier = supplier,
            VariantId = 1,
            Variant = variant,
            UnitPrice = 5,
            MinimumOrderQuantity = 10,
            LeadTimeDays = 2,
            IsAvailable = true
        });

        var receipt = await new PurchaseOrderService(uow, mapper, new FakeClaimService(10, Role.WAREHOUSE_MANAGER))
            .ReceivePurchaseOrderAsync(order.PoId, new ReceivePurchaseOrderRequest
            {
                IsFinalDelivery = true,
                Items = { new() { LineItemId = line.LineItemId, Quantity = 5, MissingQuantity = 5 } }
            });

        Assert.True(receipt.IsSuccess, receipt.ErrorMessage);
        Assert.Equal(PurchaseOrderStatus.CLOSED_WITH_VARIANCE, order.Status);
        Assert.Equal(9, requestItem.ApprovedQuantity);
        Assert.Equal(MaterialRequestStatuses.PartiallyApproved, materialRequest.Status);
        Assert.Equal(9, inventory.QuantityOnHand);
        Assert.Equal(9, inventory.ReservedQuantity);
        Assert.Equal(0, inventory.OnOrderQuantity);
        Assert.Equal(9, existingReservation.Quantity);
        Assert.Single(uow.ReservationRecords);

        var shortages = await new PurchaseOrderService(uow, mapper, new FakeClaimService(10, Role.WAREHOUSE_MANAGER))
            .GetProcurementShortagesAsync();
        var remaining = Assert.Single(Assert.IsType<List<ProcurementShortageResponse>>(shortages.Result));
        Assert.Equal(1, remaining.RemainingShortageQuantity);
        Assert.Equal(10, Assert.Single(remaining.SupplierOffers).SuggestedOrderQuantity);

        var issue = await new MaterialRequestService(uow, mapper, new FakeClaimService(10, Role.WAREHOUSE_MANAGER))
            .IssueRequestAsync(materialRequest.RequestId);
        Assert.True(issue.IsSuccess, issue.ErrorMessage);
        Assert.Equal(9, requestItem.IssuedQuantity);
        Assert.Equal(MaterialRequestStatuses.PartiallyIssued, materialRequest.Status);
        Assert.Equal(0, inventory.QuantityOnHand);
        Assert.Equal(0, inventory.ReservedQuantity);
        Assert.Equal(InventoryReservationStatuses.Fulfilled, existingReservation.Status);

        var replacementLine = new OrderLineItem
        {
            LineItemId = 2,
            VariantId = 1,
            Variant = variant,
            RequestItemId = 1,
            RequestItem = requestItem,
            Quantity = 10,
            UnitPrice = 5
        };
        var replacementOrder = new PurchaseOrder
        {
            PoId = 2,
            ProjectId = 1,
            Project = project,
            SupplierId = 1,
            Supplier = supplier,
            WarehouseId = 1,
            Warehouse = warehouse,
            UserAccountId = 10,
            Status = PurchaseOrderStatus.SHIPPED,
            TotalAmount = 50,
            OrderLineItems = new List<OrderLineItem> { replacementLine }
        };
        replacementLine.PoId = replacementOrder.PoId;
        replacementLine.PurchaseOrder = replacementOrder;
        uow.PurchaseOrderRecords.Add(replacementOrder);
        uow.OrderLineRecords.Add(replacementLine);
        inventory.OnOrderQuantity = 10;

        var replacementReceipt = await new PurchaseOrderService(uow, mapper, new FakeClaimService(10, Role.WAREHOUSE_MANAGER))
            .ReceivePurchaseOrderAsync(replacementOrder.PoId, new ReceivePurchaseOrderRequest
            {
                IsFinalDelivery = true,
                Items = { new() { LineItemId = replacementLine.LineItemId, Quantity = 10 } }
            });
        Assert.True(replacementReceipt.IsSuccess, replacementReceipt.ErrorMessage);
        Assert.Equal(10, requestItem.ApprovedQuantity);
        Assert.Equal(MaterialRequestStatuses.Approved, materialRequest.Status);
        Assert.Equal(10, inventory.QuantityOnHand);
        Assert.Equal(1, inventory.ReservedQuantity);
        Assert.Equal(9, inventory.QuantityOnHand - inventory.ReservedQuantity - inventory.QuarantineQuantity);
        Assert.Equal(2, uow.ReservationRecords.Count);

        var finalIssue = await new MaterialRequestService(uow, mapper, new FakeClaimService(10, Role.WAREHOUSE_MANAGER))
            .IssueRequestAsync(materialRequest.RequestId);
        Assert.True(finalIssue.IsSuccess, finalIssue.ErrorMessage);
        Assert.Equal(10, requestItem.IssuedQuantity);
        Assert.Equal(MaterialRequestStatuses.Issued, materialRequest.Status);
        Assert.Equal(9, inventory.QuantityOnHand);
        Assert.Equal(0, inventory.ReservedQuantity);
    }

    [Fact]
    public async Task ProgressUsesInProgressStatusInsteadOfLegacyActive()
    {
        var uow = new TestUnitOfWork();
        var task = new TaskItem { TaskId = 1, ProjectId = 1, TaskName = "T", PhaseName = "P", AssignedToUserID = 9 };
        var project = new Project
        {
            ProjectId = 1,
            ProjectName = "P",
            PMUserID = 5,
            Status = ProjectStatus.IN_PROGRESS,
            BaselineEnd = DateTime.UtcNow.AddDays(10),
            Tasks = new List<TaskItem> { task }
        };
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

    [Fact]
    public async Task PlanningProjectCannotBypassStartThroughProgress()
    {
        var uow = new TestUnitOfWork();
        var task = new TaskItem { TaskId = 1, ProjectId = 1, TaskName = "T", PhaseName = "P", AssignedToUserID = 5 };
        uow.TaskRecords.Add(task);
        uow.ProjectRecords.Add(new Project
        {
            ProjectId = 1,
            ProjectName = "P",
            PMUserID = 5,
            Status = ProjectStatus.PLANNING,
            BaselineEnd = DateTime.UtcNow.AddDays(10),
            Tasks = new List<TaskItem> { task }
        });

        var response = await new ProgressReportService(uow, CreateMapper(), new FakeClaimService(5, Role.PM))
            .SubmitReportAsync(new SubmitProgressReportRequest { TaskId = 1, ProgressIncrement = 10, ActualCostIncrement = 5 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Empty(uow.ProgressReportRecords);
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

    [Fact]
    public async Task ProjectCannotCloseWhileMaterialRequestIsOpen()
    {
        var uow = new TestUnitOfWork();
        var project = new Project
        {
            ProjectId = 1,
            ProjectName = "P",
            PMUserID = 5,
            Status = ProjectStatus.IN_PROGRESS,
            RowVersion = [1]
        };
        uow.ProjectRecords.Add(project);
        uow.RequestRecords.Add(new MaterialRequest
        {
            RequestId = 1,
            ProjectId = 1,
            Project = project,
            Status = MaterialRequestStatuses.Pending
        });

        var response = await new ProjectService(uow, CreateMapper(), new FakeClaimService(5, Role.PM))
            .ChangeProjectStatusAsync(1, "cancel", new ProjectLifecycleRequest { RowVersion = "AQ==" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(ProjectStatus.IN_PROGRESS, project.Status);
    }

    [Fact]
    public async Task ProjectDatesCannotExcludeAnExistingTask()
    {
        var uow = new TestUnitOfWork();
        var start = DateTime.UtcNow.Date;
        var project = new Project
        {
            ProjectId = 1,
            ProjectName = "P",
            PMUserID = 5,
            Status = ProjectStatus.PLANNING,
            BaselineStart = start,
            BaselineEnd = start.AddDays(30),
            StartDate = start,
            RowVersion = [1]
        };
        uow.ProjectRecords.Add(project);
        uow.TaskRecords.Add(new TaskItem
        {
            TaskId = 1,
            ProjectId = 1,
            TaskName = "T",
            PhaseName = "P",
            BaselineStart = start.AddDays(10),
            BaselineEnd = start.AddDays(20)
        });

        var response = await new ProjectService(uow, CreateMapper(), new FakeClaimService(5, Role.PM))
            .UpdateProjectAsync(1, new UpdateProjectRequest
            {
                ProjectName = "P",
                StartDate = start,
                BaselineStart = start,
                BaselineEnd = start.AddDays(15),
                RowVersion = "AQ=="
            });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(start.AddDays(30), project.BaselineEnd);
    }

    [Fact]
    public async Task ProjectBudgetCannotDropBelowActiveTaskPlan()
    {
        var uow = new TestUnitOfWork();
        uow.ProjectRecords.Add(new Project { ProjectId = 1, ProjectName = "P", PMUserID = 5, TotalProjectBudget = 100 });
        uow.TaskRecords.Add(new TaskItem
        {
            TaskId = 1,
            ProjectId = 1,
            TaskName = "T",
            PhaseName = "P",
            PlannedBudget = 80,
            Status = cpms_Domain.Models.TaskStatus.PENDING
        });

        var response = await new ProjectService(uow, CreateMapper(), new FakeClaimService(1, Role.ADMIN))
            .AdjustProjectBudgetAsync(new AdjustBudgetRequest { ProjectId = 1, Amount = -30, Reason = "Reduce" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(100, uow.ProjectRecords[0].TotalProjectBudget);
    }

    [Fact]
    public async Task ReturnedMaterialReopensTheTaskDemandWithoutErasingIssueHistory()
    {
        var uow = new TestUnitOfWork();
        var project = new Project { ProjectId = 1, ProjectName = "P", PMUserID = 5, Status = ProjectStatus.IN_PROGRESS };
        var variant = new MaterialVariant { VariantId = 1, MaterialId = 1, VariantName = "Steel", Unit = "kg", IsActive = true };
        var task = new TaskItem { TaskId = 1, ProjectId = 1, TaskName = "T", PhaseName = "P" };
        var requirement = new TaskMaterialRequirement
        {
            Id = 1,
            TaskId = 1,
            TaskItem = task,
            VariantId = 1,
            Variant = variant,
            GrossQuantityRequired = 100
        };
        task.MaterialRequirements.Add(requirement);
        var oldRequest = new MaterialRequest
        {
            RequestId = 1,
            ProjectId = 1,
            Project = project,
            TaskId = 1,
            Status = MaterialRequestStatuses.Issued
        };
        uow.ProjectRecords.Add(project);
        uow.TaskRecords.Add(task);
        uow.RequirementRecords.Add(requirement);
        uow.VariantRecords.Add(variant);
        uow.RequestRecords.Add(oldRequest);
        uow.RequisitionRecords.Add(new MaterialRequisition
        {
            ItemId = 1,
            RequestId = 1,
            MaterialRequest = oldRequest,
            VariantId = 1,
            Quantity = 40,
            ApprovedQuantity = 40,
            IssuedQuantity = 40
        });
        uow.MaterialReturnRecords.Add(new MaterialReturn
        {
            ReturnId = 1,
            MaterialRequestId = 1,
            MaterialRequest = oldRequest,
            WarehouseId = 1,
            VariantId = 1,
            Quantity = 10
        });

        var response = await new MaterialRequestService(uow, CreateMapper(), new FakeClaimService(5, Role.PM))
            .CreateRequestByTaskIdAsync(1);

        Assert.True(response.IsSuccess, response.ErrorMessage);
        var replacement = Assert.Single(uow.RequisitionRecords, item => item.ItemId != 1);
        Assert.Equal(70, replacement.Quantity);
        Assert.Equal(40, uow.RequisitionRecords.Single(item => item.ItemId == 1).IssuedQuantity);
    }

    [Fact]
    public async Task SupplierAndCatalogCanBeMaintainedWithoutDeletingHistory()
    {
        var uow = new TestUnitOfWork();
        var material = new Material { MaterialId = 1, MaterialName = "Steel", DefaultUnit = "kg", IsActive = true };
        var variant = new MaterialVariant { VariantId = 1, MaterialId = 1, Material = material, VariantName = "Grade 60", Unit = "kg", IsActive = true };
        var supplier = new Supplier { SupplierId = 1, CompanyName = "Old Supply" };
        var catalog = new SupplierCatalog
        {
            CatalogId = 1,
            SupplierId = 1,
            Supplier = supplier,
            VariantId = 1,
            Variant = variant,
            UnitPrice = 10,
            IsAvailable = true
        };
        uow.MaterialRecords.Add(material);
        uow.VariantRecords.Add(variant);
        uow.SupplierRecords.Add(supplier);
        uow.SupplierCatalogRecords.Add(catalog);

        var updated = await new CatalogService(uow, null!).UpdateCatalogOfferAsync(1, new UpdateCatalogRequest
        {
            SupplierSku = "SUP-STEEL",
            UnitPrice = 12,
            MinimumOrderQuantity = 5,
            LeadTimeDays = 2,
            IsAvailable = true
        });
        var renamed = await new SupplierService(uow, CreateMapper()).UpdateSupplierAsync(1, new UpdateSupplierRequest
        {
            CompanyName = "Reliable Supply",
            ContactEmail = "SALES@EXAMPLE.COM"
        });
        var deactivated = await new SupplierService(uow, null!).DeactivateSupplierAsync(1);

        Assert.True(updated.IsSuccess, updated.ErrorMessage);
        Assert.True(renamed.IsSuccess, renamed.ErrorMessage);
        Assert.True(deactivated.IsSuccess, deactivated.ErrorMessage);
        Assert.Equal(12, catalog.UnitPrice);
        Assert.Equal("sales@example.com", supplier.ContactEmail);
        Assert.True(supplier.IsDeleted);
        Assert.False(catalog.IsAvailable);
    }

    [Fact]
    public async Task WarehouseManagerCanBeReassignedByAdministrator()
    {
        var uow = new TestUnitOfWork();
        uow.WarehouseRecords.Add(new Warehouse { WarehouseId = 1, WarehouseName = "Old", Location = "A", ManagerId = 10 });
        uow.UserAccountRecords.Add(new UserAccount
        {
            Id = 20,
            Email = "manager@example.com",
            Role = Role.WAREHOUSE_MANAGER,
            IsEmailVerified = true
        });

        var response = await new WarehouseService(uow, CreateMapper(), new FakeClaimService(1, Role.ADMIN))
            .UpdateWarehouseAsync(1, new UpdateWarehouseRequest
            {
                ManagerId = 20,
                WarehouseName = "Main Warehouse",
                Location = "Site B"
            });

        Assert.True(response.IsSuccess, response.ErrorMessage);
        Assert.Equal(20, uow.WarehouseRecords[0].ManagerId);
        Assert.Equal("Main Warehouse", uow.WarehouseRecords[0].WarehouseName);
    }

    [Fact]
    public async Task ClosedProjectCannotApproveMaterialRequestOrPurchaseOrder()
    {
        var uow = new TestUnitOfWork();
        var project = new Project { ProjectId = 1, ProjectName = "P", PMUserID = 5, Status = ProjectStatus.CANCELLED };
        var warehouse = new Warehouse { WarehouseId = 1, WarehouseName = "W", Location = "L", ManagerId = 10 };
        var request = new MaterialRequest
        {
            RequestId = 1,
            ProjectId = 1,
            Project = project,
            Status = MaterialRequestStatuses.Pending,
            Requisitions = new List<MaterialRequisition>
            {
                new() { ItemId = 1, RequestId = 1, VariantId = 1, Quantity = 5 }
            }
        };
        var order = new PurchaseOrder
        {
            PoId = 1,
            ProjectId = 1,
            Project = project,
            WarehouseId = 1,
            Warehouse = warehouse,
            UserAccountId = 10,
            Status = PurchaseOrderStatus.PENDING
        };
        uow.ProjectRecords.Add(project);
        uow.WarehouseRecords.Add(warehouse);
        uow.RequestRecords.Add(request);
        uow.RequisitionRecords.Add(request.Requisitions.Single());
        uow.PurchaseOrderRecords.Add(order);

        var requestApproval = await new MaterialRequestService(uow, CreateMapper(), new FakeClaimService(10, Role.WAREHOUSE_MANAGER))
            .ApproveRequestAsync(1, new ApproveMaterialRequest
            {
                WarehouseId = 1,
                Items = { new() { ItemId = 1, ApprovedQuantity = 5 } }
            });
        var orderApproval = await new PurchaseOrderService(uow, CreateMapper(), new FakeClaimService(5, Role.PM))
            .ApprovePurchaseOrderAsync(1);

        Assert.Equal(HttpStatusCode.Conflict, requestApproval.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, orderApproval.StatusCode);
        Assert.Empty(uow.ReservationRecords);
    }

    [Fact]
    public async Task AvailableCatalogOfferCannotUseZeroPrice()
    {
        var uow = new TestUnitOfWork();
        uow.MaterialRecords.Add(new Material { MaterialId = 1, MaterialName = "Steel", DefaultUnit = "kg", IsActive = true });
        uow.VariantRecords.Add(new MaterialVariant { VariantId = 1, MaterialId = 1, VariantName = "Grade 60", Unit = "kg", IsActive = true });
        uow.SupplierRecords.Add(new Supplier { SupplierId = 1, CompanyName = "S" });

        var response = await new CatalogService(uow, CreateMapper()).AddMaterialToCatalogAsync(new cpms_Application.Request.SupplierCatalog.CreateCatalogRequest
        {
            SupplierId = 1,
            VariantId = 1,
            UnitPrice = 0,
            IsAvailable = true
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(uow.SupplierCatalogRecords);
    }

    [Fact]
    public async Task PartiallyIssuedRequestCanReleaseItsUnfulfilledRemainder()
    {
        var uow = new TestUnitOfWork();
        var project = new Project { ProjectId = 1, ProjectName = "P", PMUserID = 5, Status = ProjectStatus.CANCELLED };
        var warehouse = new Warehouse { WarehouseId = 1, WarehouseName = "W", Location = "L", ManagerId = 10 };
        var request = new MaterialRequest
        {
            RequestId = 1,
            ProjectId = 1,
            Project = project,
            WarehouseId = 1,
            Warehouse = warehouse,
            Status = MaterialRequestStatuses.PartiallyIssued
        };
        uow.ProjectRecords.Add(project);
        uow.WarehouseRecords.Add(warehouse);
        uow.RequestRecords.Add(request);

        var response = await new MaterialRequestService(uow, CreateMapper(), new FakeClaimService(10, Role.WAREHOUSE_MANAGER))
            .ReleaseRequestAsync(1);

        Assert.True(response.IsSuccess, response.ErrorMessage);
        Assert.Equal(MaterialRequestStatuses.Released, request.Status);
    }

    [Fact]
    public async Task CompletedProjectCannotHaveProgressReversed()
    {
        var uow = new TestUnitOfWork();
        var task = new TaskItem
        {
            TaskId = 1,
            ProjectId = 1,
            TaskName = "T",
            PhaseName = "P",
            ActualProgressPct = 100,
            ActualCost = 50,
            Status = cpms_Domain.Models.TaskStatus.COMPLETED
        };
        var project = new Project
        {
            ProjectId = 1,
            ProjectName = "P",
            PMUserID = 5,
            Status = ProjectStatus.COMPLETED,
            Tasks = new List<TaskItem> { task }
        };
        uow.ProjectRecords.Add(project);
        uow.TaskRecords.Add(task);
        uow.ProgressReportRecords.Add(new ProgressReport
        {
            ReportId = 1,
            TaskId = 1,
            Task = task,
            ProgressIncrement = 20,
            ActualCostIncrement = 10,
            Status = ProgressReportStatus.APPROVED
        });

        var response = await new ProgressReportService(uow, CreateMapper(), new FakeClaimService(5, Role.PM))
            .ReverseReportAsync(1, new ReviewProgressReportRequest());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(100, task.ActualProgressPct);
        Assert.Equal(cpms_Domain.Models.TaskStatus.COMPLETED, task.Status);
    }

    [Fact]
    public async Task ClosedProjectBudgetCannotBeRewritten()
    {
        var uow = new TestUnitOfWork();
        uow.ProjectRecords.Add(new Project
        {
            ProjectId = 1,
            ProjectName = "P",
            PMUserID = 5,
            Status = ProjectStatus.COMPLETED,
            TotalProjectBudget = 100
        });

        var response = await new ProjectService(uow, CreateMapper(), new FakeClaimService(1, Role.ADMIN))
            .AdjustProjectBudgetAsync(new AdjustBudgetRequest { ProjectId = 1, Amount = 10, Reason = "Late change" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(100, uow.ProjectRecords[0].TotalProjectBudget);
    }

    [Fact]
    public async Task PurchaseOrderCancellationKeepsAnAuditNoteAndChecksSuppliedVersion()
    {
        var uow = new TestUnitOfWork();
        var project = new Project { ProjectId = 1, ProjectName = "P", PMUserID = 5, Status = ProjectStatus.IN_PROGRESS };
        var warehouse = new Warehouse { WarehouseId = 1, WarehouseName = "W", Location = "L", ManagerId = 10 };
        var order = new PurchaseOrder
        {
            PoId = 1,
            ProjectId = 1,
            Project = project,
            WarehouseId = 1,
            Warehouse = warehouse,
            UserAccountId = 10,
            Status = PurchaseOrderStatus.PENDING,
            RowVersion = [1]
        };
        uow.ProjectRecords.Add(project);
        uow.WarehouseRecords.Add(warehouse);
        uow.PurchaseOrderRecords.Add(order);

        var stale = await new PurchaseOrderService(uow, CreateMapper(), new FakeClaimService(10, Role.WAREHOUSE_MANAGER))
            .CancelPurchaseOrderAsync(1, new PurchaseOrderActionRequest { Note = "Supplier unavailable", RowVersion = "Ag==" });
        var cancelled = await new PurchaseOrderService(uow, CreateMapper(), new FakeClaimService(10, Role.WAREHOUSE_MANAGER))
            .CancelPurchaseOrderAsync(1, new PurchaseOrderActionRequest { Note = "Supplier unavailable", RowVersion = "AQ==" });

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.True(cancelled.IsSuccess, cancelled.ErrorMessage);
        Assert.Equal(PurchaseOrderStatus.CANCELLED, order.Status);
        Assert.Contains("Supplier unavailable", order.Note);
    }

    [Fact]
    public async Task CancelledTaskCannotBeRevivedByPendingProgressApproval()
    {
        var uow = new TestUnitOfWork();
        var task = new TaskItem
        {
            TaskId = 1,
            ProjectId = 1,
            TaskName = "Foundation",
            PhaseName = "P1",
            PlannedBudget = 100,
            Status = cpms_Domain.Models.TaskStatus.CANCELLED
        };
        var project = new Project
        {
            ProjectId = 1,
            ProjectName = "P",
            PMUserID = 5,
            Status = ProjectStatus.IN_PROGRESS,
            Tasks = new List<TaskItem> { task }
        };
        var report = new ProgressReport
        {
            ReportId = 1,
            TaskId = 1,
            Task = task,
            Status = ProgressReportStatus.PENDING,
            ProgressIncrement = 10
        };
        uow.ProjectRecords.Add(project);
        uow.TaskRecords.Add(task);
        uow.ProgressReportRecords.Add(report);

        var response = await new ProgressReportService(uow, CreateMapper(), new FakeClaimService(5, Role.PM))
            .ApproveReportAsync(1, new ReviewProgressReportRequest { AllowCostOverrun = true });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(cpms_Domain.Models.TaskStatus.CANCELLED, task.Status);
        Assert.Equal(ProgressReportStatus.PENDING, report.Status);
    }

    [Fact]
    public async Task TaskCannotCloseWhileItsMaterialRequestIsOpen()
    {
        var uow = new TestUnitOfWork();
        var project = new Project { ProjectId = 1, ProjectName = "P", PMUserID = 5, Status = ProjectStatus.IN_PROGRESS };
        var task = new TaskItem
        {
            TaskId = 1,
            ProjectId = 1,
            TaskName = "Foundation",
            PhaseName = "P1",
            Status = cpms_Domain.Models.TaskStatus.PENDING,
            RowVersion = [1]
        };
        uow.ProjectRecords.Add(project);
        uow.TaskRecords.Add(task);
        uow.RequestRecords.Add(new MaterialRequest
        {
            RequestId = 1,
            TaskId = 1,
            ProjectId = 1,
            Status = MaterialRequestStatuses.PartiallyApproved
        });

        var response = await new TaskService(uow, CreateMapper(), new FakeClaimService(5, Role.PM))
            .ChangeTaskStatusAsync(1, "cancel", new TaskLifecycleRequest { RowVersion = "AQ==" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(cpms_Domain.Models.TaskStatus.PENDING, task.Status);
    }

    [Fact]
    public async Task MrpExcludesCancelledTaskDemand()
    {
        var uow = new TestUnitOfWork();
        var task = new TaskItem
        {
            TaskId = 1,
            ProjectId = 1,
            Status = cpms_Domain.Models.TaskStatus.CANCELLED,
            ActualProgressPct = 0
        };
        var variant = new MaterialVariant
        {
            VariantId = 1,
            MaterialId = 1,
            Material = new Material { MaterialId = 1, MaterialName = "Steel", DefaultUnit = "kg" },
            VariantName = "Grade 60",
            Unit = "kg"
        };
        uow.ProjectRecords.Add(new Project { ProjectId = 1, ProjectName = "P", PMUserID = 5, Status = ProjectStatus.IN_PROGRESS });
        uow.WarehouseRecords.Add(new Warehouse { WarehouseId = 1, WarehouseName = "W", ManagerId = 10 });
        uow.TaskRecords.Add(task);
        uow.RequirementRecords.Add(new TaskMaterialRequirement
        {
            TaskId = 1,
            TaskItem = task,
            VariantId = 1,
            Variant = variant,
            GrossQuantityRequired = 100
        });

        var response = await new ProjectService(uow, CreateMapper(), new FakeClaimService(5, Role.PM))
            .CalculateMRPForProjectAsync(1, 1);

        Assert.True(response.IsSuccess, response.ErrorMessage);
        Assert.Empty(Assert.IsType<List<MRPCalculationResponse>>(response.Result));
    }

    [Fact]
    public async Task PausedProjectRejectsNewMaterialAndPurchaseCommitments()
    {
        var uow = new TestUnitOfWork();
        var project = new Project { ProjectId = 1, ProjectName = "P", PMUserID = 5, Status = ProjectStatus.PAUSED };
        var warehouse = new Warehouse { WarehouseId = 1, WarehouseName = "W", ManagerId = 10 };
        uow.ProjectRecords.Add(project);
        uow.WarehouseRecords.Add(warehouse);
        uow.SupplierRecords.Add(new Supplier { SupplierId = 1, CompanyName = "Supplier" });

        var materialResponse = await new MaterialRequestService(uow, CreateMapper(), new FakeClaimService(5, Role.PM))
            .CreateRequestAsync(new CreateMaterialRequest { ProjectId = 1, TaskId = 1, Items = { new() { VariantId = 1, Quantity = 1 } } });
        var purchaseResponse = await new PurchaseOrderService(uow, CreateMapper(), new FakeClaimService(10, Role.WAREHOUSE_MANAGER))
            .CreatePurchaseOrderAsync(new CreatePurchaseOrderRequest
            {
                ProjectId = 1,
                WarehouseId = 1,
                SupplierId = 1,
                Items = { new() { VariantId = 1, Quantity = 1 } }
            });

        Assert.Equal(HttpStatusCode.Conflict, materialResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, purchaseResponse.StatusCode);
        Assert.Empty(uow.RequestRecords);
        Assert.Empty(uow.PurchaseOrderRecords);
    }

    [Fact]
    public async Task PendingRequestEditUsesNetIssuedAfterReturns()
    {
        var uow = new TestUnitOfWork();
        var project = new Project { ProjectId = 1, ProjectName = "P", PMUserID = 5, Status = ProjectStatus.IN_PROGRESS };
        var pending = new MaterialRequest
        {
            RequestId = 2,
            ProjectId = 1,
            Project = project,
            TaskId = 1,
            Status = MaterialRequestStatuses.Pending,
            RowVersion = [1]
        };
        var pendingItem = new MaterialRequisition { ItemId = 2, RequestId = 2, MaterialRequest = pending, VariantId = 1, Quantity = 40 };
        pending.Requisitions.Add(pendingItem);
        var oldRequest = new MaterialRequest { RequestId = 1, ProjectId = 1, Project = project, TaskId = 1, Status = MaterialRequestStatuses.Released };
        uow.ProjectRecords.Add(project);
        uow.RequestRecords.AddRange(new[] { oldRequest, pending });
        uow.RequisitionRecords.AddRange(new[]
        {
            new MaterialRequisition { ItemId = 1, RequestId = 1, MaterialRequest = oldRequest, VariantId = 1, Quantity = 60, IssuedQuantity = 60 },
            pendingItem
        });
        uow.RequirementRecords.Add(new TaskMaterialRequirement { TaskId = 1, VariantId = 1, GrossQuantityRequired = 100 });
        uow.MaterialReturnRecords.Add(new MaterialReturn { ReturnId = 1, MaterialRequestId = 1, MaterialRequest = oldRequest, VariantId = 1, Quantity = 20 });

        var response = await new MaterialRequestService(uow, CreateMapper(), new FakeClaimService(5, Role.PM))
            .UpdatePendingRequestAsync(2, new UpdatePendingMaterialRequest
            {
                RowVersion = "AQ==",
                Items = { new() { ItemId = 2, Quantity = 60, NeededByDate = DateTime.UtcNow.Date } }
            });

        Assert.True(response.IsSuccess, response.ErrorMessage);
        Assert.Equal(60, pendingItem.Quantity);
    }

    [Fact]
    public async Task MaterialRequestResponseShowsSkuReturnsAndNetDemand()
    {
        var uow = new TestUnitOfWork();
        var project = new Project { ProjectId = 1, ProjectName = "P", PMUserID = 5 };
        var material = new Material { MaterialId = 1, MaterialName = "Steel", DefaultUnit = "kg" };
        var variant = new MaterialVariant
        {
            VariantId = 1,
            MaterialId = 1,
            Material = material,
            VariantName = "Grade 60",
            SKU = "STL-G60",
            Unit = "kg"
        };
        var request = new MaterialRequest { RequestId = 1, ProjectId = 1, Project = project, TaskId = 1, Status = MaterialRequestStatuses.Issued };
        var item = new MaterialRequisition
        {
            ItemId = 1,
            RequestId = 1,
            MaterialRequest = request,
            VariantId = 1,
            Variant = variant,
            Quantity = 60,
            ApprovedQuantity = 60,
            IssuedQuantity = 60
        };
        request.Requisitions.Add(item);
        uow.ProjectRecords.Add(project);
        uow.RequestRecords.Add(request);
        uow.RequisitionRecords.Add(item);
        uow.RequirementRecords.Add(new TaskMaterialRequirement { TaskId = 1, VariantId = 1, GrossQuantityRequired = 100 });
        uow.MaterialReturnRecords.Add(new MaterialReturn { ReturnId = 1, MaterialRequestId = 1, MaterialRequest = request, VariantId = 1, Quantity = 20 });

        var response = await new MaterialRequestService(uow, CreateMapper(), new FakeClaimService(5, Role.PM))
            .GetRequestByIdAsync(1);

        Assert.True(response.IsSuccess, response.ErrorMessage);
        var resultItem = Assert.Single(Assert.IsType<MaterialRequestResponse>(response.Result).Items);
        Assert.Equal("STL-G60", resultItem.SKU);
        Assert.Equal(20, resultItem.ReturnedQuantity);
        Assert.Equal(40, resultItem.NetIssuedQuantity);
        Assert.Equal(60, resultItem.RemainingTaskDemand);
    }

    [Fact]
    public async Task RejectedTaskDoesNotBlockProjectCompletion()
    {
        var uow = new TestUnitOfWork();
        var completed = new TaskItem { TaskId = 1, ProjectId = 1, Status = cpms_Domain.Models.TaskStatus.COMPLETED };
        var rejected = new TaskItem { TaskId = 2, ProjectId = 1, Status = cpms_Domain.Models.TaskStatus.REJECTED };
        var project = new Project
        {
            ProjectId = 1,
            ProjectName = "P",
            PMUserID = 5,
            Status = ProjectStatus.IN_PROGRESS,
            RowVersion = [1],
            Tasks = new List<TaskItem> { completed, rejected }
        };
        uow.ProjectRecords.Add(project);
        uow.TaskRecords.AddRange(new[] { completed, rejected });

        var response = await new ProjectService(uow, CreateMapper(), new FakeClaimService(5, Role.PM))
            .ChangeProjectStatusAsync(1, "complete", new ProjectLifecycleRequest { RowVersion = "AQ==" });

        Assert.True(response.IsSuccess, response.ErrorMessage);
        Assert.Equal(ProjectStatus.COMPLETED, project.Status);
    }

    [Fact]
    public async Task ProcessingPurchaseOrderRejectsSuppliedStaleVersion()
    {
        var uow = new TestUnitOfWork();
        var project = new Project { ProjectId = 1, ProjectName = "P", Status = ProjectStatus.IN_PROGRESS };
        var warehouse = new Warehouse { WarehouseId = 1, WarehouseName = "W", ManagerId = 10 };
        var order = new PurchaseOrder
        {
            PoId = 1,
            ProjectId = 1,
            Project = project,
            WarehouseId = 1,
            Warehouse = warehouse,
            Status = PurchaseOrderStatus.APPROVED,
            RowVersion = [1]
        };
        uow.PurchaseOrderRecords.Add(order);

        var response = await new PurchaseOrderService(uow, CreateMapper(), new FakeClaimService(10, Role.WAREHOUSE_MANAGER))
            .MarkProcessingAsync(1, new PurchaseOrderActionRequest { RowVersion = "Ag==" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(PurchaseOrderStatus.APPROVED, order.Status);
    }

    private static IMapper CreateMapper() => new MapperConfiguration(configuration =>
        configuration.AddProfile<MapperConfigurationsProfile>(), NullLoggerFactory.Instance).CreateMapper();
}
