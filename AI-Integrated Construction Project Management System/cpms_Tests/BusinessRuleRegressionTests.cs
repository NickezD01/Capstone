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
using cpms_Application.Response.Project;
using cpms_Application.Response.PurchaseOrder;
using cpms_Application.Response.SupplierCatalog;
using cpms_Application.Response.Tasks;
using cpms_Application.Response.UserAccount;
using cpms_Application.Response;
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
    public async Task AdministratorCanCreateVerifiedAccount()
    {
        var uow = new TestUnitOfWork();

        var response = await new UserAccountService(uow, CreateMapper(), new FakeClaimService(1, Role.ADMIN))
            .CreateAccountAsync(new CreateUserAccountRequest
            {
                FirstName = "Pat",
                LastName = "Manager",
                Email = "pm@example.com",
                Role = Role.PM,
                Password = "StrongPass123",
                ConfirmPassword = "StrongPass123"
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = Assert.Single(uow.UserAccountRecords);
        Assert.Equal("pm@example.com", created.Email);
        Assert.Equal(Role.PM, created.Role);
        Assert.True(created.IsEmailVerified);
        Assert.NotEmpty(created.PasswordHash);
    }

    [Fact]
    public async Task CreateAccountRejectsDuplicateWeakPasswordAndSupplier()
    {
        var uow = new TestUnitOfWork();
        uow.UserAccountRecords.Add(new UserAccount
        {
            Id = 20,
            Email = "pm@example.com",
            IsEmailVerified = true,
            Role = Role.CUSTOMER
        });
        var service = new UserAccountService(uow, CreateMapper(), new FakeClaimService(1, Role.ADMIN));

        var duplicate = await service.CreateAccountAsync(new CreateUserAccountRequest
        {
            FirstName = "Pat",
            LastName = "Manager",
            Email = "PM@EXAMPLE.COM",
            Role = Role.PM,
            Password = "StrongPass123",
            ConfirmPassword = "StrongPass123"
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var weak = await service.CreateAccountAsync(new CreateUserAccountRequest
        {
            FirstName = "Pat",
            LastName = "Manager",
            Email = "new@example.com",
            Role = Role.PM,
            Password = "weak",
            ConfirmPassword = "weak"
        });
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);

        var supplier = await service.CreateAccountAsync(new CreateUserAccountRequest
        {
            FirstName = "Sup",
            LastName = "Plier",
            Email = "supplier@example.com",
            Role = Role.SUPPLIER,
            Password = "StrongPass123",
            ConfirmPassword = "StrongPass123"
        });
        Assert.Equal(HttpStatusCode.BadRequest, supplier.StatusCode);
        Assert.Equal(1, uow.UserAccountRecords.Count);
    }

    [Fact]
    public async Task CustomerListReturnsOnlyVerifiedCustomersWithSafeFields()
    {
        var uow = new TestUnitOfWork();
        uow.UserAccountRecords.AddRange(new[]
        {
            new UserAccount { Id = 20, Role = Role.CUSTOMER, IsEmailVerified = true, FirstName = "Cara", LastName = "Client", Email = "cara@example.com", PhoneNumber = "0123" },
            new UserAccount { Id = 21, Role = Role.CUSTOMER, IsEmailVerified = false, FirstName = "Unverified", LastName = "User", Email = "unverified@example.com" },
            new UserAccount { Id = 22, Role = Role.PM, IsEmailVerified = true, FirstName = "Pat", LastName = "Manager", Email = "pm@example.com" }
        });

        var response = await new UserAccountService(uow, CreateMapper(), new FakeClaimService(5, Role.PM))
            .GetCustomersAsync(null);

        Assert.True(response.IsSuccess, response.ErrorMessage);
        var customers = Assert.IsType<List<CustomerListResponse>>(response.Result);
        var customer = Assert.Single(customers);
        Assert.Equal(20, customer.Id);
        Assert.Equal("Cara", customer.FirstName);
        Assert.Equal("cara@example.com", customer.Email);
    }

    [Fact]
    public async Task CustomerListSearchFiltersByNameOrEmail()
    {
        var uow = new TestUnitOfWork();
        uow.UserAccountRecords.AddRange(new[]
        {
            new UserAccount { Id = 20, Role = Role.CUSTOMER, IsEmailVerified = true, FirstName = "Cara", LastName = "Client", Email = "cara@example.com" },
            new UserAccount { Id = 21, Role = Role.CUSTOMER, IsEmailVerified = true, FirstName = "Dan", LastName = "Builder", Email = "dan@example.com" }
        });
        var service = new UserAccountService(uow, CreateMapper(), new FakeClaimService(5, Role.PM));

        var byName = await service.GetCustomersAsync("dan");
        Assert.Equal(21, Assert.Single(Assert.IsType<List<CustomerListResponse>>(byName.Result)).Id);

        var byEmail = await service.GetCustomersAsync("CARA@EXAMPLE.COM");
        Assert.Equal(20, Assert.Single(Assert.IsType<List<CustomerListResponse>>(byEmail.Result)).Id);

        var none = await service.GetCustomersAsync("nobody");
        Assert.Empty(Assert.IsType<List<CustomerListResponse>>(none.Result));
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
    public async Task ApprovalUsesActiveWarehouse()
    {
        var uow = new TestUnitOfWork();
        var active = new Warehouse { WarehouseId = 1, WarehouseName = "Active", ManagerId = 10 };
        uow.WarehouseRecords.AddRange(new[]
        {
            active,
            new Warehouse { WarehouseId = 2, WarehouseName = "Retired", ManagerId = 20, IsActive = false }
        });
        var project = new Project { ProjectId = 1, ProjectName = "P", PMUserID = 5, Status = ProjectStatus.IN_PROGRESS };
        var variant = new MaterialVariant { VariantId = 1, MaterialId = 1, VariantName = "Steel", Unit = "kg", IsActive = true };
        var item = new MaterialRequisition { ItemId = 1, RequestId = 1, VariantId = 1, Variant = variant, Quantity = 5 };
        var request = new MaterialRequest
        {
            RequestId = 1,
            ProjectId = 1,
            Project = project,
            Warehouse = active,
            Status = MaterialRequestStatuses.Pending,
            Requisitions = new List<MaterialRequisition> { item }
        };
        item.MaterialRequest = request;
        uow.ProjectRecords.Add(project);
        uow.VariantRecords.Add(variant);
        uow.InventoryRecords.Add(new InventoryRecord { InventoryId = 1, WarehouseId = 1, VariantId = 1, QuantityOnHand = 10 });
        uow.RequestRecords.Add(request);
        uow.RequisitionRecords.Add(item);

        var response = await new MaterialRequestService(uow, CreateMapper(), new FakeClaimService(10, Role.WAREHOUSE_MANAGER))
            .ApproveRequestAsync(1, new ApproveMaterialRequest
            {
                Items = { new() { ItemId = 1, ApprovedQuantity = 5 } }
            });

        Assert.True(response.IsSuccess, response.ErrorMessage);
        Assert.Equal(1, request.WarehouseId);
        Assert.Equal(5, uow.InventoryRecords.Single().ReservedQuantity);
        Assert.Single(uow.ReservationRecords);
    }

    [Fact]
    public async Task ApprovalRequiresTheActiveWarehouseManager()
    {
        var uow = new TestUnitOfWork();
        uow.WarehouseRecords.AddRange(new[]
        {
            new Warehouse { WarehouseId = 1, WarehouseName = "Active", ManagerId = 10 },
            new Warehouse { WarehouseId = 2, WarehouseName = "Retired", ManagerId = 20, IsActive = false }
        });
        uow.RequestRecords.Add(new MaterialRequest
        {
            RequestId = 1,
            ProjectId = 1,
            Status = MaterialRequestStatuses.Pending,
            Requisitions = new List<MaterialRequisition>
            {
                new() { ItemId = 1, RequestId = 1, VariantId = 1, Quantity = 5 }
            }
        });

        var response = await new MaterialRequestService(uow, CreateMapper(), new FakeClaimService(20, Role.WAREHOUSE_MANAGER))
            .ApproveRequestAsync(1, new ApproveMaterialRequest
            {
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
        uow.PhaseRecords.Add(new Phase
        {
            PhaseId = 1,
            ProjectId = 1,
            Name = "P",
            BaselineStart = start,
            BaselineEnd = start.AddMonths(2),
            Status = PhaseStatus.PLANNED
        });
        uow.TaskRecords.Add(new TaskItem { TaskId = 1, ProjectId = 1, PhaseId = 1, TaskName = "Existing", PhaseName = "P", PlannedBudget = 80 });

        var response = await new TaskService(uow, null!, new FakeClaimService(5, Role.PM)).CreateTaskAsync(1, new CreateTaskRequest
        {
            AssignedToUserID = 9,
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
        uow.PhaseRecords.Add(new Phase
        {
            PhaseId = 1,
            ProjectId = 1,
            Name = "Foundation",
            BaselineStart = start,
            BaselineEnd = start.AddMonths(2),
            Status = PhaseStatus.PLANNED
        });
        uow.UserAccountRecords.Add(new UserAccount
        {
            Id = 9,
            Email = "worker@example.com",
            IsEmailVerified = true,
            Role = Role.WORKER
        });

        var response = await new TaskService(uow, CreateMapper(), new FakeClaimService(5, Role.PM))
            .CreateTaskAsync(1, new CreateTaskRequest
            {
                AssignedToUserID = 9,
                TaskName = "Excavation",
                PlannedBudget = 100,
                BaselineStart = start,
                BaselineEnd = start.AddDays(10)
            });

        Assert.True(response.IsSuccess, response.ErrorMessage);
        var task = Assert.Single(uow.TaskRecords);
        Assert.Equal(1, task.PhaseId);
        Assert.Equal("Foundation", task.PhaseName);
        Assert.Equal(5, task.AssignedToUserID);
        Assert.Equal(0, task.ActualCost);
        Assert.Equal(0, task.ActualProgressPct);
        Assert.Equal(cpms_Domain.Models.TaskStatus.PENDING, task.Status);

        var body = Assert.IsType<TaskResponse>(response.Result);
        Assert.Equal(1, body.PhaseId);
        Assert.Equal("Foundation", body.PhaseName);
        Assert.NotNull(body.Phase);
        Assert.Equal(1, body.Phase!.PhaseId);
        Assert.Equal("Foundation", body.Phase.Name);
        Assert.Equal("PLANNED", body.Phase.Status);
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
        uow.PhaseRecords.Add(new Phase
        {
            PhaseId = 1,
            ProjectId = 1,
            Name = "Foundation",
            BaselineStart = start,
            BaselineEnd = start.AddMonths(2),
            Status = PhaseStatus.PLANNED
        });

        var response = await new TaskService(uow, CreateMapper(), new FakeClaimService(6, Role.PM))
            .CreateTaskAsync(1, new CreateTaskRequest
            {
                AssignedToUserID = 6,
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
        uow.PhaseRecords.Add(new Phase
        {
            PhaseId = 1,
            ProjectId = 1,
            Name = "New phase",
            BaselineStart = start,
            BaselineEnd = start.AddMonths(2),
            Status = PhaseStatus.PLANNED
        });
        var task = new TaskItem
        {
            TaskId = 1,
            ProjectId = 1,
            PhaseId = 1,
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
                PhaseId = 1,
                AssignedToUserID = 11,
                TaskName = "New task",
                PlannedBudget = 75,
                BaselineStart = start.AddDays(1),
                BaselineEnd = start.AddDays(8),
                RowVersion = Convert.ToBase64String(rowVersion)
            });

        Assert.True(response.IsSuccess, response.ErrorMessage);
        Assert.Equal(5, task.AssignedToUserID);
        Assert.Equal("New task", task.TaskName);
        Assert.Equal("New phase", task.PhaseName);
        Assert.Equal(75, task.PlannedBudget);

        var body = Assert.IsType<TaskResponse>(response.Result);
        Assert.NotNull(body.Phase);
        Assert.Equal(1, body.Phase!.PhaseId);
        Assert.Equal("New phase", body.Phase.Name);
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
        uow.PhaseRecords.Add(new Phase
        {
            PhaseId = 1,
            ProjectId = 1,
            Name = "Foundation",
            BaselineStart = start,
            BaselineEnd = start.AddMonths(2),
            Status = PhaseStatus.PLANNED
        });

        var response = await new TaskService(uow, CreateMapper(), new FakeClaimService(5, Role.PM))
            .CreateTaskAsync(1, new CreateTaskRequest
            {
                AssignedToUserID = 5,
                TaskName = "Excavation",
                PlannedBudget = 100,
                BaselineStart = start.AddDays(-1),
                BaselineEnd = start.AddDays(10)
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(uow.TaskRecords);
    }

    [Fact]
    public async Task TaskBaselineDatesMustRemainInsidePhaseBaseline()
    {
        var uow = new TestUnitOfWork();
        var start = DateTime.UtcNow.Date;
        uow.ProjectRecords.Add(new Project
        {
            ProjectId = 1,
            ProjectName = "P",
            PMUserID = 5,
            BaselineStart = start,
            BaselineEnd = start.AddMonths(3)
        });
        uow.PhaseRecords.Add(new Phase
        {
            PhaseId = 1,
            ProjectId = 1,
            Name = "Foundation",
            BaselineStart = start.AddDays(5),
            BaselineEnd = start.AddDays(20),
            Status = PhaseStatus.PLANNED
        });

        var response = await new TaskService(uow, CreateMapper(), new FakeClaimService(5, Role.PM))
            .CreateTaskAsync(1, new CreateTaskRequest
            {
                AssignedToUserID = 5,
                TaskName = "Excavation",
                PlannedBudget = 100,
                BaselineStart = start,
                BaselineEnd = start.AddDays(15)
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("phase baseline", response.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(uow.TaskRecords);
    }

    [Fact]
    public async Task CreateTask_RejectsClosedOrCancelledPhase()
    {
        var uow = new TestUnitOfWork();
        var start = DateTime.UtcNow.Date;
        uow.ProjectRecords.Add(new Project
        {
            ProjectId = 1,
            ProjectName = "P",
            PMUserID = 5,
            BaselineStart = start,
            BaselineEnd = start.AddMonths(3)
        });
        uow.PhaseRecords.Add(new Phase
        {
            PhaseId = 1,
            ProjectId = 1,
            Name = "Completed Phase",
            BaselineStart = start,
            BaselineEnd = start.AddMonths(1),
            Status = PhaseStatus.COMPLETED
        });

        var response = await new TaskService(uow, CreateMapper(), new FakeClaimService(5, Role.PM))
            .CreateTaskAsync(1, new CreateTaskRequest
            {
                AssignedToUserID = 5,
                TaskName = "Late Task",
                PlannedBudget = 50,
                BaselineStart = start.AddDays(1),
                BaselineEnd = start.AddDays(5)
            });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("Closed or cancelled phases", response.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateTask_RejectsPhaseFromAnotherProject()
    {
        var uow = new TestUnitOfWork();
        var start = DateTime.UtcNow.Date;
        var rowVersion = new byte[] { 1, 2, 3 };
        uow.ProjectRecords.Add(new Project
        {
            ProjectId = 1,
            ProjectName = "P1",
            PMUserID = 5,
            BaselineStart = start,
            BaselineEnd = start.AddMonths(2)
        });
        uow.ProjectRecords.Add(new Project
        {
            ProjectId = 2,
            ProjectName = "P2",
            PMUserID = 5,
            BaselineStart = start,
            BaselineEnd = start.AddMonths(2)
        });
        uow.PhaseRecords.Add(new Phase
        {
            PhaseId = 1,
            ProjectId = 1,
            Name = "P1 Phase",
            BaselineStart = start,
            BaselineEnd = start.AddMonths(1),
            Status = PhaseStatus.PLANNED
        });
        uow.PhaseRecords.Add(new Phase
        {
            PhaseId = 2,
            ProjectId = 2,
            Name = "P2 Phase",
            BaselineStart = start,
            BaselineEnd = start.AddMonths(1),
            Status = PhaseStatus.PLANNED
        });
        var task = new TaskItem
        {
            TaskId = 1,
            ProjectId = 1,
            PhaseId = 1,
            AssignedToUserID = 5,
            PhaseName = "P1 Phase",
            TaskName = "Task in P1",
            PlannedBudget = 50,
            BaselineStart = start,
            BaselineEnd = start.AddDays(5),
            RowVersion = rowVersion
        };
        uow.TaskRecords.Add(task);

        var response = await new TaskService(uow, CreateMapper(), new FakeClaimService(5, Role.PM))
            .UpdateTaskAsync(1, new UpdateTaskRequest
            {
                PhaseId = 2,
                AssignedToUserID = 5,
                TaskName = "Moved Task",
                PlannedBudget = 50,
                BaselineStart = start,
                BaselineEnd = start.AddDays(5),
                RowVersion = Convert.ToBase64String(rowVersion)
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Target phase does not belong to this project", response.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DeprecatedCreateTask_Returns410Gone()
    {
        var controller = new cpms_API.Controllers.TaskController(null!);
        var result = controller.DeprecatedCreateTask() as Microsoft.AspNetCore.Mvc.ObjectResult;
        Assert.NotNull(result);
        Assert.Equal(410, result!.StatusCode);
        var apiResponse = result.Value as ApiResponse;
        Assert.NotNull(apiResponse);
        Assert.False(apiResponse!.IsSuccess);
        Assert.Contains("POST /api/Phases/{phaseId}/tasks", apiResponse.ErrorMessage);
    }
    [Fact]
    public async Task CatalogOffersExposeSupplierTerms()
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

        var response = await new ProjectService(uow, CreateMapper(), new FakeClaimService(5, Role.PM))
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
    public async Task UpdatingAWarehouseIsRetired()
    {
        var uow = new TestUnitOfWork();
        uow.WarehouseRecords.Add(new Warehouse { WarehouseId = 1, WarehouseName = "Old", Location = "A", ManagerId = 10 });

        var response = await new WarehouseService(uow, CreateMapper(), new FakeClaimService(10, Role.WAREHOUSE_MANAGER))
            .UpdateWarehouseAsync(1, new UpdateWarehouseRequest
            {
                ManagerId = 20,
                WarehouseName = "Main Warehouse",
                Location = "Site B"
            });

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
        Assert.Equal(10, uow.WarehouseRecords[0].ManagerId);
        Assert.Equal("Old", uow.WarehouseRecords[0].WarehouseName);
    }

    [Fact]
    public async Task CreatingAdditionalWarehousesIsRetired()
    {
        var uow = new TestUnitOfWork();

        var response = await new WarehouseService(uow, CreateMapper(), new FakeClaimService(1, Role.ADMIN))
            .CreateWarehouseAsync(new CreateWarehouseRequest
            {
                ManagerId = 10,
                WarehouseName = "Another",
                Location = "Site C"
            });

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
        Assert.Empty(uow.WarehouseRecords);
    }

    [Fact]
    public async Task ClosedProjectCannotApproveMaterialRequest()
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
        uow.ProjectRecords.Add(project);
        uow.WarehouseRecords.Add(warehouse);
        uow.RequestRecords.Add(request);
        uow.RequisitionRecords.Add(request.Requisitions.Single());

        var requestApproval = await new MaterialRequestService(uow, CreateMapper(), new FakeClaimService(10, Role.WAREHOUSE_MANAGER))
            .ApproveRequestAsync(1, new ApproveMaterialRequest
            {
                Items = { new() { ItemId = 1, ApprovedQuantity = 5 } }
            });

        Assert.Equal(HttpStatusCode.Conflict, requestApproval.StatusCode);
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

        var response = await new ProjectService(uow, CreateMapper(), new FakeClaimService(5, Role.PM))
            .AdjustProjectBudgetAsync(new AdjustBudgetRequest { ProjectId = 1, Amount = 10, Reason = "Late change" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(100, uow.ProjectRecords[0].TotalProjectBudget);
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
    public async Task PausedProjectRejectsNewMaterialRequests()
    {
        var uow = new TestUnitOfWork();
        var project = new Project { ProjectId = 1, ProjectName = "P", PMUserID = 5, Status = ProjectStatus.PAUSED };
        var warehouse = new Warehouse { WarehouseId = 1, WarehouseName = "W", ManagerId = 10 };
        uow.ProjectRecords.Add(project);
        uow.WarehouseRecords.Add(warehouse);

        var materialResponse = await new MaterialRequestService(uow, CreateMapper(), new FakeClaimService(5, Role.PM))
            .CreateRequestAsync(new CreateMaterialRequest { ProjectId = 1, TaskId = 1, Items = { new() { VariantId = 1, Quantity = 1 } } });

        Assert.Equal(HttpStatusCode.Conflict, materialResponse.StatusCode);
        Assert.Empty(uow.RequestRecords);
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
    public async Task OwningPmAssignsVerifiedCustomerToProject()
    {
        var uow = new TestUnitOfWork();
        var project = new Project { ProjectId = 1, ProjectName = "P", PMUserID = 5, Status = ProjectStatus.IN_PROGRESS, RowVersion = [1] };
        var customer = new UserAccount { Id = 20, Role = Role.CUSTOMER, IsEmailVerified = true, FirstName = "Cara", LastName = "Client" };
        uow.ProjectRecords.Add(project);
        uow.UserAccountRecords.Add(customer);

        var response = await new ProjectService(uow, CreateMapper(), new FakeClaimService(5, Role.PM))
            .AssignCustomerAsync(1, new AssignCustomerRequest { CustomerUserId = 20, RowVersion = "AQ==" });

        Assert.True(response.IsSuccess, response.ErrorMessage);
        Assert.Equal(20, project.CustomerUserId);
        Assert.Equal("Client Cara", Assert.IsType<ProjectResponse>(response.Result).CustomerName);
    }

    [Fact]
    public async Task AssignCustomerRejectsNonCustomerAccount()
    {
        var uow = new TestUnitOfWork();
        var project = new Project { ProjectId = 1, ProjectName = "P", PMUserID = 5, Status = ProjectStatus.IN_PROGRESS, RowVersion = [1] };
        var manager = new UserAccount { Id = 20, Role = Role.PM, IsEmailVerified = true };
        uow.ProjectRecords.Add(project);
        uow.UserAccountRecords.Add(manager);

        var response = await new ProjectService(uow, CreateMapper(), new FakeClaimService(5, Role.PM))
            .AssignCustomerAsync(1, new AssignCustomerRequest { CustomerUserId = 20, RowVersion = "AQ==" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(project.CustomerUserId);
    }

    [Fact]
    public async Task NonOwningPmCannotAssignCustomer()
    {
        var uow = new TestUnitOfWork();
        var project = new Project { ProjectId = 1, ProjectName = "P", PMUserID = 5, Status = ProjectStatus.IN_PROGRESS, RowVersion = [1] };
        uow.ProjectRecords.Add(project);
        uow.UserAccountRecords.Add(new UserAccount { Id = 20, Role = Role.CUSTOMER, IsEmailVerified = true });

        var response = await new ProjectService(uow, CreateMapper(), new FakeClaimService(6, Role.PM))
            .AssignCustomerAsync(1, new AssignCustomerRequest { CustomerUserId = 20, RowVersion = "AQ==" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null(project.CustomerUserId);
    }

    [Fact]
    public async Task ClosedProjectRejectsCustomerAssignment()
    {
        var uow = new TestUnitOfWork();
        var project = new Project { ProjectId = 1, ProjectName = "P", PMUserID = 5, Status = ProjectStatus.COMPLETED, RowVersion = [1] };
        uow.ProjectRecords.Add(project);
        uow.UserAccountRecords.Add(new UserAccount { Id = 20, Role = Role.CUSTOMER, IsEmailVerified = true });

        var response = await new ProjectService(uow, CreateMapper(), new FakeClaimService(5, Role.PM))
            .AssignCustomerAsync(1, new AssignCustomerRequest { CustomerUserId = 20, RowVersion = "AQ==" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Null(project.CustomerUserId);
    }

    [Fact]
    public async Task AssignedCustomerCanReadProjectButAnotherCustomerCannot()
    {
        var uow = new TestUnitOfWork();
        uow.ProjectRecords.Add(new Project { ProjectId = 1, ProjectName = "P", PMUserID = 5, Status = ProjectStatus.IN_PROGRESS, CustomerUserId = 20 });

        var assigned = await new ProjectService(uow, CreateMapper(), new FakeClaimService(20, Role.CUSTOMER))
            .GetProjectByIdAsync(1);
        var stranger = await new ProjectService(uow, CreateMapper(), new FakeClaimService(21, Role.CUSTOMER))
            .GetProjectByIdAsync(1);

        Assert.True(assigned.IsSuccess, assigned.ErrorMessage);
        Assert.Equal(HttpStatusCode.Forbidden, stranger.StatusCode);
    }

    [Fact]
    public async Task AssignedCustomerCanListPhasesAndTasks()
    {
        var uow = new TestUnitOfWork();
        uow.ProjectRecords.Add(new Project { ProjectId = 1, ProjectName = "P", PMUserID = 5, Status = ProjectStatus.IN_PROGRESS, CustomerUserId = 20 });
        uow.PhaseRecords.Add(new Phase
        {
            PhaseId = 1,
            ProjectId = 1,
            Name = "Foundation",
            SequenceOrder = 0,
            BaselineStart = DateTime.UtcNow.Date,
            BaselineEnd = DateTime.UtcNow.Date.AddDays(10)
        });
        uow.TaskRecords.Add(new TaskItem
        {
            TaskId = 1,
            ProjectId = 1,
            PhaseId = 1,
            PhaseName = "Foundation",
            TaskName = "Excavate",
            BaselineStart = DateTime.UtcNow.Date,
            BaselineEnd = DateTime.UtcNow.Date.AddDays(5)
        });

        var phases = await new PhaseService(uow, CreateMapper(), new FakeClaimService(20, Role.CUSTOMER))
            .GetPhasesByProjectAsync(1);
        var tasks = await new TaskService(uow, CreateMapper(), new FakeClaimService(20, Role.CUSTOMER))
            .GetTasksByProjectAsync(1);
        var strangerPhases = await new PhaseService(uow, CreateMapper(), new FakeClaimService(21, Role.CUSTOMER))
            .GetPhasesByProjectAsync(1);
        var strangerTasks = await new TaskService(uow, CreateMapper(), new FakeClaimService(21, Role.CUSTOMER))
            .GetTasksByProjectAsync(1);

        Assert.True(phases.IsSuccess, phases.ErrorMessage);
        Assert.True(tasks.IsSuccess, tasks.ErrorMessage);
        Assert.Equal(HttpStatusCode.Forbidden, strangerPhases.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, strangerTasks.StatusCode);
    }

    [Fact]
    public async Task LinkedWarehouseManagerCanReadTaskProgressReports()
    {
        var uow = new TestUnitOfWork();
        var project = new Project { ProjectId = 1, ProjectName = "P", PMUserID = 5, Status = ProjectStatus.IN_PROGRESS };
        var warehouse = new Warehouse { WarehouseId = 1, WarehouseName = "W", ManagerId = 10 };
        var task = new TaskItem
        {
            TaskId = 1,
            ProjectId = 1,
            Project = project,
            TaskName = "T",
            PhaseName = "P",
            AssignedToUserID = 5,
            BaselineStart = DateTime.UtcNow.Date,
            BaselineEnd = DateTime.UtcNow.Date.AddDays(5)
        };
        var reporter = new UserAccount { Id = 5, Role = Role.PM, IsEmailVerified = true, FirstName = "Pat", LastName = "Manager" };
        var report = new ProgressReport
        {
            ReportId = 1,
            TaskId = 1,
            Task = task,
            ReportedByUserId = 5,
            Reporter = reporter,
            ReportDate = DateTime.UtcNow,
            ProgressIncrement = 25,
            Status = ProgressReportStatus.APPROVED
        };
        uow.ProjectRecords.Add(project);
        uow.TaskRecords.Add(task);
        uow.UserAccountRecords.Add(reporter);
        uow.WarehouseRecords.Add(warehouse);
        uow.ProgressReportRecords.Add(report);
        uow.RequestRecords.Add(new MaterialRequest
        {
            RequestId = 1,
            ProjectId = 1,
            WarehouseId = 1,
            Warehouse = warehouse,
            Status = MaterialRequestStatuses.Approved
        });

        var linked = await new ProgressReportService(uow, CreateMapper(), new FakeClaimService(10, Role.WAREHOUSE_MANAGER))
            .GetReportsByTaskIdAsync(1);
        var unlinked = await new ProgressReportService(uow, CreateMapper(), new FakeClaimService(99, Role.WAREHOUSE_MANAGER))
            .GetReportsByTaskIdAsync(1);

        Assert.True(linked.IsSuccess, linked.ErrorMessage);
        Assert.Equal(HttpStatusCode.Forbidden, unlinked.StatusCode);
    }

    [Fact]
    public async Task GetAllProjectsReturnsOnlyAssignedProjectsForCustomer()
    {
        var uow = new TestUnitOfWork();
        uow.ProjectRecords.Add(new Project { ProjectId = 1, ProjectName = "Mine", PMUserID = 5, Status = ProjectStatus.IN_PROGRESS, CustomerUserId = 20 });
        uow.ProjectRecords.Add(new Project { ProjectId = 2, ProjectName = "Other", PMUserID = 6, Status = ProjectStatus.IN_PROGRESS, CustomerUserId = 21 });

        var response = await new ProjectService(uow, CreateMapper(), new FakeClaimService(20, Role.CUSTOMER))
            .GetAllProjectsAsync();

        Assert.True(response.IsSuccess, response.ErrorMessage);
        var projects = Assert.IsType<List<ProjectResponse>>(response.Result);
        Assert.Equal(1, Assert.Single(projects).ProjectId);
    }

    [Fact]
    public async Task CreateProjectRejectsNonCustomerAssignment()
    {
        var uow = new TestUnitOfWork();
        uow.UserAccountRecords.Add(new UserAccount { Id = 5, Role = Role.PM, IsEmailVerified = true, FirstName = "Pat", LastName = "Manager" });
        uow.UserAccountRecords.Add(new UserAccount { Id = 20, Role = Role.WORKER, IsEmailVerified = true });

        var response = await new ProjectService(uow, CreateMapper(), new FakeClaimService(5, Role.PM))
            .CreateProjectAsync(new CreateProjectRequest
            {
                ProjectName = "P",
                PMUserID = 5,
                StartDate = DateTime.UtcNow.Date,
                BaselineStart = DateTime.UtcNow.Date,
                BaselineEnd = DateTime.UtcNow.Date.AddDays(30),
                CustomerUserId = 20
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(uow.ProjectRecords);
    }

    [Fact]
    public async Task AdministratorCannotChangeProjectStatus()
    {
        var uow = new TestUnitOfWork();
        var project = new Project
        {
            ProjectId = 1,
            ProjectName = "P",
            PMUserID = 5,
            Status = ProjectStatus.PLANNING,
            RowVersion = [1]
        };
        uow.ProjectRecords.Add(project);

        var response = await new ProjectService(uow, CreateMapper(), new FakeClaimService(1, Role.ADMIN))
            .ChangeProjectStatusAsync(1, "start", new ProjectLifecycleRequest { RowVersion = "AQ==" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(ProjectStatus.PLANNING, project.Status);
    }

    [Fact]
    public async Task NonOwningPmCannotAdjustProjectBudget()
    {
        var uow = new TestUnitOfWork();
        var project = new Project
        {
            ProjectId = 1,
            ProjectName = "P",
            PMUserID = 5,
            Status = ProjectStatus.IN_PROGRESS,
            TotalProjectBudget = 100,
            RowVersion = [1]
        };
        uow.ProjectRecords.Add(project);

        var response = await new ProjectService(uow, CreateMapper(), new FakeClaimService(6, Role.PM))
            .AdjustProjectBudgetAsync(new AdjustBudgetRequest { ProjectId = 1, Amount = 10, Reason = "Extra" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(100, project.TotalProjectBudget);
    }

    [Fact]
    public async Task WarehouseManagerCanApproveOwnInventoryAdjustment()
    {
        var uow = new TestUnitOfWork();
        uow.WarehouseRecords.Add(new Warehouse { WarehouseId = 1, WarehouseName = "W", ManagerId = 10 });
        var material = new Material { MaterialId = 1, MaterialName = "Steel", DefaultUnit = "kg" };
        var variant = new MaterialVariant { VariantId = 1, MaterialId = 1, Material = material, VariantName = "Standard", Unit = "kg", IsActive = true };
        uow.VariantRecords.Add(variant);
        var inventory = new InventoryRecord { InventoryId = 1, WarehouseId = 1, VariantId = 1, QuantityOnHand = 10, Variant = variant };
        uow.InventoryRecords.Add(inventory);
        var adjustment = new InventoryAdjustment
        {
            AdjustmentId = 1,
            WarehouseId = 1,
            VariantId = 1,
            QuantityDelta = 5,
            Status = InventoryAdjustmentStatuses.Pending,
            RequestedByUserId = 10,
            RowVersion = [1]
        };
        uow.AdjustmentRecords.Add(adjustment);

        var response = await new WarehouseService(uow, CreateMapper(), new FakeClaimService(10, Role.WAREHOUSE_MANAGER))
            .ReviewInventoryAdjustmentAsync(1, true, new ReviewInventoryAdjustmentRequest { RowVersion = "AQ==" });

        Assert.True(response.IsSuccess, response.ErrorMessage);
        Assert.Equal(15, inventory.QuantityOnHand);
        Assert.Equal(InventoryAdjustmentStatuses.Approved, adjustment.Status);
        Assert.Equal(10, adjustment.ReviewedByUserId);
    }

    [Fact]
    public async Task InventoryAdjustmentReviewForbiddenForNonManagingManagerAndAdministrator()
    {
        var uow = new TestUnitOfWork();
        uow.WarehouseRecords.Add(new Warehouse { WarehouseId = 1, WarehouseName = "W", ManagerId = 10 });
        var adjustment = new InventoryAdjustment
        {
            AdjustmentId = 1,
            WarehouseId = 1,
            VariantId = 1,
            QuantityDelta = 5,
            Status = InventoryAdjustmentStatuses.Pending,
            RequestedByUserId = 10,
            RowVersion = [1]
        };
        uow.AdjustmentRecords.Add(adjustment);
        var review = new ReviewInventoryAdjustmentRequest { RowVersion = "AQ==" };

        var otherManager = await new WarehouseService(uow, CreateMapper(), new FakeClaimService(11, Role.WAREHOUSE_MANAGER))
            .ReviewInventoryAdjustmentAsync(1, true, review);
        var admin = await new WarehouseService(uow, CreateMapper(), new FakeClaimService(1, Role.ADMIN))
            .ReviewInventoryAdjustmentAsync(1, true, review);

        Assert.Equal(HttpStatusCode.Forbidden, otherManager.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, admin.StatusCode);
        Assert.Equal(InventoryAdjustmentStatuses.Pending, adjustment.Status);
    }

    [Fact]
    public async Task WarehouseManagerCanApproveOwnPhysicalCount()
    {
        var uow = new TestUnitOfWork();
        var warehouse = new Warehouse { WarehouseId = 1, WarehouseName = "W", ManagerId = 10 };
        uow.WarehouseRecords.Add(warehouse);
        var material = new Material { MaterialId = 1, MaterialName = "Steel", DefaultUnit = "kg" };
        var variant = new MaterialVariant { VariantId = 1, MaterialId = 1, Material = material, VariantName = "Standard", Unit = "kg", IsActive = true };
        uow.VariantRecords.Add(variant);
        var inventory = new InventoryRecord { InventoryId = 1, WarehouseId = 1, VariantId = 1, QuantityOnHand = 8, Variant = variant, RowVersion = [7] };
        uow.InventoryRecords.Add(inventory);
        var line = new PhysicalCountLine
        {
            LineId = 1,
            SessionId = 1,
            InventoryId = 1,
            VariantId = 1,
            ExpectedQuantity = 8,
            ActualQuantity = 12,
            ExpectedInventoryRowVersion = [7],
            InventoryRecord = inventory
        };
        var session = new PhysicalCountSession
        {
            SessionId = 1,
            WarehouseId = 1,
            Warehouse = warehouse,
            CreatedByUserId = 10,
            Status = PhysicalCountStatuses.PendingApproval,
            RowVersion = [2],
            Lines = new List<PhysicalCountLine> { line }
        };
        uow.PhysicalCountSessionRecords.Add(session);

        var response = await new WarehouseService(uow, CreateMapper(), new FakeClaimService(10, Role.WAREHOUSE_MANAGER))
            .ReviewPhysicalCountAsync(1, true, new ReviewPhysicalCountRequest { RowVersion = "Ag==" });

        Assert.True(response.IsSuccess, response.ErrorMessage);
        Assert.Equal(12, inventory.QuantityOnHand);
        Assert.Equal(PhysicalCountStatuses.Approved, session.Status);
        Assert.Equal(10, session.ReviewedByUserId);
    }

    [Fact]
    public async Task CreateRequestStoresEstimateWithoutDebiting()
    {
        var uow = CreateLedgerFixture(out _, out _, out _);
        uow.RequestRecords.Clear();
        uow.RequisitionRecords.Clear();

        var response = await new MaterialRequestService(uow, CreateMapper(), new FakeClaimService(5, Role.PM))
            .CreateRequestAsync(new CreateMaterialRequest
            {
                ProjectId = 1,
                TaskId = 1,
                EstimatedCost = 500,
                Items = { new() { VariantId = 1, Quantity = 5, NeededByDate = DateTime.UtcNow.Date } }
            });

        Assert.True(response.IsSuccess, response.ErrorMessage);
        var created = Assert.Single(uow.RequestRecords);
        Assert.Equal(500, created.EstimatedCost);
        Assert.Equal(0, created.ActualCost);
        Assert.Equal(0, created.BudgetDebitedAmount);
        Assert.Empty(uow.MaterialBudgetTransactionRecords);
    }

    [Fact]
    public async Task CreateRequestRejectsNegativeEstimate()
    {
        var uow = CreateLedgerFixture(out _, out _, out _);
        uow.RequestRecords.Clear();
        uow.RequisitionRecords.Clear();

        var response = await new MaterialRequestService(uow, CreateMapper(), new FakeClaimService(5, Role.PM))
            .CreateRequestAsync(new CreateMaterialRequest
            {
                ProjectId = 1,
                TaskId = 1,
                EstimatedCost = -1,
                Items = { new() { VariantId = 1, Quantity = 5, NeededByDate = DateTime.UtcNow.Date } }
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(uow.RequestRecords);
    }

    [Fact]
    public async Task PendingEstimateCanBeUpdatedButNeverDebits()
    {
        var uow = CreateLedgerFixture(out var request, out _, out _);

        var response = await new MaterialRequestService(uow, CreateMapper(), new FakeClaimService(5, Role.PM))
            .UpdatePendingRequestAsync(1, new UpdatePendingMaterialRequest
            {
                RowVersion = string.Empty,
                EstimatedCost = 700,
                Items = { new() { ItemId = 1, Quantity = 5, NeededByDate = DateTime.UtcNow.Date } }
            });

        Assert.True(response.IsSuccess, response.ErrorMessage);
        Assert.Equal(700, request.EstimatedCost);
        Assert.Equal(0, request.BudgetDebitedAmount);
        Assert.Empty(uow.MaterialBudgetTransactionRecords);

        var negative = await new MaterialRequestService(uow, CreateMapper(), new FakeClaimService(5, Role.PM))
            .UpdatePendingRequestAsync(1, new UpdatePendingMaterialRequest
            {
                RowVersion = string.Empty,
                EstimatedCost = -5,
                Items = { new() { ItemId = 1, Quantity = 5, NeededByDate = DateTime.UtcNow.Date } }
            });

        Assert.Equal(HttpStatusCode.BadRequest, negative.StatusCode);
        Assert.Equal(700, request.EstimatedCost);
    }

    [Fact]
    public async Task ApproveRecordsPerLineUnitCostAndRollupWithoutDebiting()
    {
        var uow = CreateLedgerFixture(out var request, out var item, out _);

        var response = await new MaterialRequestService(uow, CreateMapper(), new FakeClaimService(10, Role.WAREHOUSE_MANAGER))
            .ApproveRequestAsync(1, new ApproveMaterialRequest
            {
                Items = { new() { ItemId = 1, ApprovedQuantity = 5, UnitActualCost = 12 } }
            });

        Assert.True(response.IsSuccess, response.ErrorMessage);
        Assert.Equal(12, item.UnitActualCost);
        Assert.Equal(60, request.ActualCost);
        Assert.Equal(10, request.ActualCostUpdatedByUserId);
        Assert.NotNull(request.ActualCostUpdatedAt);
        Assert.Equal(0, request.BudgetDebitedAmount);
        Assert.Empty(uow.MaterialBudgetTransactionRecords);
    }

    [Fact]
    public async Task IssuePostsExactlyOnceDebit()
    {
        var uow = CreateLedgerFixture(out var request, out _, out var task);
        await ApproveLedgerRequestAsync(uow, unitCost: 10);

        var first = await new MaterialRequestService(uow, CreateMapper(), new FakeClaimService(10, Role.WAREHOUSE_MANAGER))
            .IssueRequestAsync(1);

        Assert.True(first.IsSuccess, first.ErrorMessage);
        Assert.Equal(50, request.BudgetDebitedAmount);
        Assert.Equal(50, task.ActualCost);
        var entry = Assert.Single(uow.MaterialBudgetTransactionRecords);
        Assert.Equal(MaterialBudgetTransactionTypes.IssueDebit, entry.TransactionType);
        Assert.Equal(50, entry.Amount);
        Assert.Equal(0, entry.DebitedBefore);
        Assert.Equal(50, entry.DebitedAfter);
        Assert.Equal(10, entry.PerformedByUserId);
        Assert.Equal(MaterialRequestStatuses.Issued, request.Status);

        var second = await new MaterialRequestService(uow, CreateMapper(), new FakeClaimService(10, Role.WAREHOUSE_MANAGER))
            .IssueRequestAsync(1);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Single(uow.MaterialBudgetTransactionRecords);
        Assert.Equal(50, request.BudgetDebitedAmount);
    }

    [Fact]
    public async Task PartialIssuePostsPartialDebitsAndReissuesRemainder()
    {
        var uow = CreateLedgerFixture(out var request, out _, out var task, requestQty: 10);
        await ApproveLedgerRequestAsync(uow, unitCost: 10, approvedQty: 10);

        var partial = await new MaterialRequestService(uow, CreateMapper(), new FakeClaimService(10, Role.WAREHOUSE_MANAGER))
            .IssueRequestAsync(1, new IssueMaterialRequest
            {
                Items = { new() { ItemId = 1, Quantity = 4 } }
            });

        Assert.True(partial.IsSuccess, partial.ErrorMessage);
        Assert.Equal(40, request.BudgetDebitedAmount);
        Assert.Equal(40, task.ActualCost);
        Assert.Equal(MaterialRequestStatuses.PartiallyIssued, request.Status);
        Assert.Single(uow.MaterialBudgetTransactionRecords);

        var remainder = await new MaterialRequestService(uow, CreateMapper(), new FakeClaimService(10, Role.WAREHOUSE_MANAGER))
            .IssueRequestAsync(1);

        Assert.True(remainder.IsSuccess, remainder.ErrorMessage);
        Assert.Equal(100, request.BudgetDebitedAmount);
        Assert.Equal(100, task.ActualCost);
        Assert.Equal(MaterialRequestStatuses.Issued, request.Status);
        Assert.Equal(2, uow.MaterialBudgetTransactionRecords.Count);
        Assert.Equal(100, uow.MaterialBudgetTransactionRecords.Sum(t => t.Amount));
    }

    [Fact]
    public async Task IssueBlockedWhenExceedingApprovedBudget()
    {
        var uow = CreateLedgerFixture(out var request, out _, out _, projectBudget: 100, requestQty: 10);
        var inventory = uow.InventoryRecords.Single();
        await ApproveLedgerRequestAsync(uow, unitCost: 20, approvedQty: 10);

        var response = await new MaterialRequestService(uow, CreateMapper(), new FakeClaimService(10, Role.WAREHOUSE_MANAGER))
            .IssueRequestAsync(1);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(0, request.BudgetDebitedAmount);
        Assert.Empty(uow.MaterialBudgetTransactionRecords);
        Assert.Equal(20, inventory.QuantityOnHand);
        Assert.Equal(MaterialRequestStatuses.Approved, request.Status);
    }

    [Fact]
    public async Task CorrectionPostsDeltaOnly()
    {
        var uow = CreateLedgerFixture(out var request, out _, out var task);
        await ApproveLedgerRequestAsync(uow, unitCost: 10);
        var issuer = new MaterialRequestService(uow, CreateMapper(), new FakeClaimService(10, Role.WAREHOUSE_MANAGER));
        Assert.True((await issuer.IssueRequestAsync(1)).IsSuccess);

        var corrected = await new MaterialRequestService(uow, CreateMapper(), new FakeClaimService(10, Role.WAREHOUSE_MANAGER))
            .AdjustActualCostAsync(1, new AdjustActualCostRequest
            {
                RowVersion = string.Empty,
                Items = { new() { ItemId = 1, UnitActualCost = 14 } }
            });

        Assert.True(corrected.IsSuccess, corrected.ErrorMessage);
        Assert.Equal(70, request.BudgetDebitedAmount);
        Assert.Equal(70, task.ActualCost);
        Assert.Equal(70, request.ActualCost);
        Assert.Equal(2, uow.MaterialBudgetTransactionRecords.Count);
        var delta = uow.MaterialBudgetTransactionRecords.Single(t => t.TransactionType == MaterialBudgetTransactionTypes.CorrectionDelta);
        Assert.Equal(20, delta.Amount);
        Assert.Equal(50, delta.DebitedBefore);
        Assert.Equal(70, delta.DebitedAfter);

        var noop = await new MaterialRequestService(uow, CreateMapper(), new FakeClaimService(10, Role.WAREHOUSE_MANAGER))
            .AdjustActualCostAsync(1, new AdjustActualCostRequest
            {
                RowVersion = string.Empty,
                Items = { new() { ItemId = 1, UnitActualCost = 14 } }
            });

        Assert.True(noop.IsSuccess, noop.ErrorMessage);
        Assert.Equal(2, uow.MaterialBudgetTransactionRecords.Count);
    }

    [Fact]
    public async Task CorrectionAppliesToOutstandingOnlyAndFullReturnZeroesLedger()
    {
        var uow = CreateLedgerFixture(out var request, out _, out var task);
        await ApproveLedgerRequestAsync(uow, unitCost: 10);
        var warehouseService = new WarehouseService(uow, CreateMapper(), new FakeClaimService(10, Role.WAREHOUSE_MANAGER));
        var materialService = new MaterialRequestService(uow, CreateMapper(), new FakeClaimService(10, Role.WAREHOUSE_MANAGER));
        Assert.True((await materialService.IssueRequestAsync(1)).IsSuccess);
        Assert.True((await warehouseService.ReturnInventoryAsync(new InventoryReturnRequest
        {
            VariantId = 1,
            MaterialRequestId = 1,
            Quantity = 2
        })).IsSuccess);
        foreach (var materialReturn in uow.MaterialReturnRecords)
            materialReturn.MaterialRequest ??= request;
        Assert.Equal(30, request.BudgetDebitedAmount);

        var corrected = await materialService.AdjustActualCostAsync(1, new AdjustActualCostRequest
        {
            RowVersion = string.Empty,
            Items = { new() { ItemId = 1, UnitActualCost = 14 } }
        });

        Assert.True(corrected.IsSuccess, corrected.ErrorMessage);
        Assert.Equal(42, request.BudgetDebitedAmount);

        var finalReturn = await warehouseService.ReturnInventoryAsync(new InventoryReturnRequest
        {
            VariantId = 1,
            MaterialRequestId = 1,
            Quantity = 3
        });

        Assert.True(finalReturn.IsSuccess, finalReturn.ErrorMessage);
        Assert.Equal(0, request.BudgetDebitedAmount);
        Assert.Equal(0, task.ActualCost);
        Assert.Equal(0, uow.MaterialBudgetTransactionRecords.Sum(t => t.Amount));
    }

    [Fact]
    public async Task ReturnPostsExplicitReversal()
    {
        var uow = CreateLedgerFixture(out var request, out _, out var task);
        await ApproveLedgerRequestAsync(uow, unitCost: 10);
        Assert.True((await new MaterialRequestService(uow, CreateMapper(), new FakeClaimService(10, Role.WAREHOUSE_MANAGER))
            .IssueRequestAsync(1)).IsSuccess);

        var response = await new WarehouseService(uow, CreateMapper(), new FakeClaimService(10, Role.WAREHOUSE_MANAGER))
            .ReturnInventoryAsync(new InventoryReturnRequest
            {
                VariantId = 1,
                MaterialRequestId = 1,
                Quantity = 2
            });

        Assert.True(response.IsSuccess, response.ErrorMessage);
        Assert.Equal(30, request.BudgetDebitedAmount);
        Assert.Equal(30, task.ActualCost);
        var reversal = Assert.Single(uow.MaterialBudgetTransactionRecords, t => t.TransactionType == MaterialBudgetTransactionTypes.ReturnReversal);
        Assert.Equal(-20, reversal.Amount);
        Assert.Equal(50, reversal.DebitedBefore);
        Assert.Equal(30, reversal.DebitedAfter);
        Assert.Equal(2, reversal.Quantity);
    }

    [Fact]
    public async Task StaleRowVersionConflictsOnIssueAndAdjust()
    {
        var uow = CreateLedgerFixture(out var request, out _, out _);
        request.RowVersion = new byte[] { 9 };
        await ApproveLedgerRequestAsync(uow, unitCost: 10);

        var issue = await new MaterialRequestService(uow, CreateMapper(), new FakeClaimService(10, Role.WAREHOUSE_MANAGER))
            .IssueRequestAsync(1, new IssueMaterialRequest { RowVersion = "AQ==" });

        Assert.Equal(HttpStatusCode.Conflict, issue.StatusCode);
        Assert.Empty(uow.MaterialBudgetTransactionRecords);

        var adjust = await new MaterialRequestService(uow, CreateMapper(), new FakeClaimService(10, Role.WAREHOUSE_MANAGER))
            .AdjustActualCostAsync(1, new AdjustActualCostRequest
            {
                RowVersion = "AQ==",
                Items = { new() { ItemId = 1, UnitActualCost = 11 } }
            });

        Assert.Equal(HttpStatusCode.Conflict, adjust.StatusCode);
        Assert.Empty(uow.MaterialBudgetTransactionRecords);
    }

    [Fact]
    public async Task ReleaseCreatesNoBudgetEntry()
    {
        var uow = CreateLedgerFixture(out var request, out _, out _);
        await ApproveLedgerRequestAsync(uow, unitCost: 10);

        var response = await new MaterialRequestService(uow, CreateMapper(), new FakeClaimService(10, Role.WAREHOUSE_MANAGER))
            .ReleaseRequestAsync(1);

        Assert.True(response.IsSuccess, response.ErrorMessage);
        Assert.Equal(MaterialRequestStatuses.Released, request.Status);
        Assert.Equal(0, request.BudgetDebitedAmount);
        Assert.Empty(uow.MaterialBudgetTransactionRecords);
    }

    private static TestUnitOfWork CreateLedgerFixture(
        out MaterialRequest request,
        out MaterialRequisition item,
        out TaskItem task,
        decimal projectBudget = 100000,
        decimal requestQty = 5)
    {
        var uow = new TestUnitOfWork();
        var project = new Project { ProjectId = 1, ProjectName = "P", PMUserID = 5, Status = ProjectStatus.IN_PROGRESS, TotalProjectBudget = projectBudget };
        task = new TaskItem { TaskId = 1, ProjectId = 1, Project = project, TaskName = "T", PhaseName = "P" };
        var warehouse = new Warehouse { WarehouseId = 1, WarehouseName = "Main", ManagerId = 10 };
        var material = new Material { MaterialId = 1, MaterialName = "Steel", DefaultUnit = "kg" };
        var variant = new MaterialVariant { VariantId = 1, MaterialId = 1, Material = material, VariantName = "Grade 60", Unit = "kg", IsActive = true };
        item = new MaterialRequisition
        {
            ItemId = 1,
            RequestId = 1,
            VariantId = 1,
            Variant = variant,
            Quantity = requestQty,
            NeededByDate = DateTime.UtcNow.Date
        };
        request = new MaterialRequest
        {
            RequestId = 1,
            ProjectId = 1,
            Project = project,
            TaskId = 1,
            Warehouse = warehouse,
            Status = MaterialRequestStatuses.Pending,
            Requisitions = new List<MaterialRequisition> { item }
        };
        item.MaterialRequest = request;
        uow.ProjectRecords.Add(project);
        uow.TaskRecords.Add(task);
        uow.WarehouseRecords.Add(warehouse);
        uow.VariantRecords.Add(variant);
        uow.RequirementRecords.Add(new TaskMaterialRequirement { Id = 1, TaskId = 1, VariantId = 1, GrossQuantityRequired = 100 });
        uow.InventoryRecords.Add(new InventoryRecord
        {
            InventoryId = 1,
            WarehouseId = 1,
            Warehouse = warehouse,
            VariantId = 1,
            Variant = variant,
            QuantityOnHand = 20,
            AverageUnitCost = 10
        });
        uow.RequestRecords.Add(request);
        uow.RequisitionRecords.Add(item);
        return uow;
    }

    private static async Task ApproveLedgerRequestAsync(TestUnitOfWork uow, decimal unitCost, decimal? approvedQty = null)
    {
        var item = uow.RequisitionRecords.Single();
        var response = await new MaterialRequestService(uow, CreateMapper(), new FakeClaimService(10, Role.WAREHOUSE_MANAGER))
            .ApproveRequestAsync(1, new ApproveMaterialRequest
            {
                Items = { new() { ItemId = 1, ApprovedQuantity = approvedQty ?? item.Quantity, UnitActualCost = unitCost } }
            });
        Assert.True(response.IsSuccess, response.ErrorMessage);
        var request = uow.RequestRecords.Single();
        foreach (var reservation in uow.ReservationRecords.Where(r => r.RequestId == 1))
        {
            reservation.InventoryRecord ??= uow.InventoryRecords.Single(i => i.InventoryId == reservation.InventoryId);
            reservation.RequestItem ??= uow.RequisitionRecords.Single(i => i.ItemId == reservation.RequestItemId);
            reservation.MaterialRequest ??= request;
            if (request.Reservations.All(r => r.ReservationId != reservation.ReservationId))
                request.Reservations.Add(reservation);
        }
    }

    private static IMapper CreateMapper() => new MapperConfiguration(configuration =>
        configuration.AddProfile<MapperConfigurationsProfile>(), NullLoggerFactory.Instance).CreateMapper();
}
