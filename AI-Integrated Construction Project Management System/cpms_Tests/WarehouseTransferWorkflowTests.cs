using AutoMapper;
using cpms_Application;
using cpms_Application.Interfaces;
using cpms_Application.Repository;
using cpms_Application.Request.WarehouseTransfer;
using cpms_Application.Response.MaterialRequest;
using cpms_Application.Services;
using cpms_Domain.Models;
using Microsoft.EntityFrameworkCore.Query;
using System.Data;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;

namespace cpms_Tests;

public class WarehouseTransferWorkflowTests
{
    [Fact]
    public async Task CannotTransferToSameWarehouse()
    {
        var (service, _) = CreateTransferService(managerId: 10);
        var response = await service.CreateAsync(new CreateWarehouseTransferRequest
        {
            SourceWarehouseId = 1,
            DestinationWarehouseId = 1,
            Items = { new() { VariantId = 1, Quantity = 1 } }
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(10, 0, 11)]
    [InlineData(10, 6, 5)]
    public async Task ApprovalRejectsMoreThanAvailableOrReservedStock(decimal onHand, decimal reserved, decimal requested)
    {
        var (service, uow) = CreateTransferService(managerId: 20, status: WarehouseTransferStatuses.Requested, requested: requested);
        uow.InventoryRecords.Add(new InventoryRecord { InventoryId = 1, WarehouseId = 1, VariantId = 1, QuantityOnHand = onHand, ReservedQuantity = reserved });

        var response = await service.ApproveAsync(1);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(WarehouseTransferStatuses.Requested, uow.TransferRecords.Single().Status);
    }

    [Fact]
    public async Task ApprovalReservesStockAndCancellationReleasesIt()
    {
        var (service, uow) = CreateTransferService(managerId: 20, requested: 4);
        var source = new InventoryRecord { InventoryId = 1, WarehouseId = 1, VariantId = 1, QuantityOnHand = 10, ReservedQuantity = 2 };
        uow.InventoryRecords.Add(source);

        var approved = await service.ApproveAsync(1);
        Assert.True(approved.IsSuccess);
        Assert.Equal(6, source.ReservedQuantity);

        var sourceService = new WarehouseTransferService(uow, new FakeClaimService(10, Role.WAREHOUSE_MANAGER));
        var cancelled = await sourceService.CancelAsync(1);
        Assert.True(cancelled.IsSuccess);
        Assert.Equal(2, source.ReservedQuantity);
        Assert.Equal(WarehouseTransferStatuses.Cancelled, uow.TransferRecords.Single().Status);
    }

    [Fact]
    public async Task ShippingDecreasesOnlySourceAndCreatesTransferOut()
    {
        var (service, uow) = CreateTransferService(managerId: 10, status: WarehouseTransferStatuses.Approved, requested: 4);
        var source = new InventoryRecord { InventoryId = 1, WarehouseId = 1, VariantId = 1, QuantityOnHand = 10, ReservedQuantity = 7 };
        var destination = new InventoryRecord { InventoryId = 2, WarehouseId = 2, VariantId = 1, QuantityOnHand = 8 };
        uow.InventoryRecords.AddRange(new[] { source, destination });
        uow.TransferReservationRecords.Add(new TransferInventoryReservation
        {
            TransferReservationId = 1,
            TransferId = 1,
            TransferItemId = 1,
            InventoryId = 1,
            Quantity = 4,
            Status = TransferReservationStatuses.Active
        });

        var response = await service.ShipAsync(1);

        Assert.True(response.IsSuccess);
        Assert.Equal(6, source.QuantityOnHand);
        Assert.Equal(3, source.ReservedQuantity);
        Assert.Equal(8, destination.QuantityOnHand);
        var transaction = Assert.Single(uow.TransactionRecords);
        Assert.Equal(InventoryTransactionTypes.TransferOut, transaction.TransactionType);
        Assert.Equal(-4, transaction.Quantity);
        Assert.Equal(1, transaction.ReferenceId);
        Assert.Equal("WAREHOUSE_TRANSFER", transaction.ReferenceType);
    }

    [Fact]
    public async Task ShippingCannotConsumeAnotherWorkflowReservationWhenTransferLedgerIsMissing()
    {
        var (service, uow) = CreateTransferService(managerId: 10, status: WarehouseTransferStatuses.Approved, requested: 4);
        var source = new InventoryRecord
        {
            InventoryId = 1,
            WarehouseId = 1,
            VariantId = 1,
            QuantityOnHand = 10,
            ReservedQuantity = 4
        };
        uow.InventoryRecords.Add(source);

        var response = await service.ShipAsync(1);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(10, source.QuantityOnHand);
        Assert.Equal(4, source.ReservedQuantity);
        Assert.Empty(uow.TransactionRecords);
    }

    [Fact]
    public async Task ReceivingCreatesDestinationInventoryAndMatchingTransferIn()
    {
        var (service, uow) = CreateTransferService(managerId: 20, status: WarehouseTransferStatuses.InTransit, requested: 4, shipped: 4);
        uow.InventoryRecords.Add(new InventoryRecord { InventoryId = 1, WarehouseId = 1, VariantId = 1, QuantityOnHand = 6 });

        var response = await service.ReceiveAsync(1, null);

        Assert.True(response.IsSuccess);
        Assert.Equal(6, uow.InventoryRecords.Single(x => x.WarehouseId == 1).QuantityOnHand);
        var destination = Assert.Single(uow.InventoryRecords, x => x.WarehouseId == 2);
        Assert.Equal(4, destination.QuantityOnHand);
        var transaction = Assert.Single(uow.TransactionRecords);
        Assert.Equal(InventoryTransactionTypes.TransferIn, transaction.TransactionType);
        Assert.Equal(1, transaction.ReferenceId);
        Assert.Equal("WAREHOUSE_TRANSFER", transaction.ReferenceType);
        Assert.Equal(WarehouseTransferStatuses.Received, uow.TransferRecords.Single().Status);
    }

    [Fact]
    public async Task UnrelatedManagerCannotApproveAndInvalidTransitionConflicts()
    {
        var (unauthorized, _) = CreateTransferService(managerId: 99, status: WarehouseTransferStatuses.Requested);
        Assert.Equal(HttpStatusCode.Forbidden, (await unauthorized.ApproveAsync(1)).StatusCode);

        var (invalidState, _) = CreateTransferService(managerId: 20, status: WarehouseTransferStatuses.InTransit);
        Assert.Equal(HttpStatusCode.Conflict, (await invalidState.ApproveAsync(1)).StatusCode);
    }

    [Fact]
    public async Task WarehouseScopedMrpExcludesOtherWarehouseInventory()
    {
        var uow = new TestUnitOfWork();
        uow.ProjectRecords.Add(new Project { ProjectId = 1, ProjectName = "P", PMUserID = 1 });
        uow.WarehouseRecords.AddRange(new[]
        {
            new Warehouse { WarehouseId = 1, WarehouseName = "W1", ManagerId = 10 },
            new Warehouse { WarehouseId = 2, WarehouseName = "W2", ManagerId = 20 }
        });
        var material = new Material { MaterialId = 1, MaterialName = "Steel", DefaultUnit = "kg" };
        var variant = new MaterialVariant { VariantId = 1, MaterialId = 1, Material = material, VariantName = "Standard", Unit = "kg", IsActive = true };
        var task = new TaskItem { TaskId = 1, ProjectId = 1, TaskName = "T", PhaseName = "P", BaselineStart = DateTime.UtcNow, ActualProgressPct = 0 };
        uow.RequirementRecords.Add(new TaskMaterialRequirement { Id = 1, TaskId = 1, TaskItem = task, VariantId = 1, Variant = variant, GrossQuantityRequired = 20 });
        uow.InventoryRecords.AddRange(new[]
        {
            new InventoryRecord { InventoryId = 1, WarehouseId = 1, VariantId = 1, QuantityOnHand = 5 },
            new InventoryRecord { InventoryId = 2, WarehouseId = 2, VariantId = 1, QuantityOnHand = 100 }
        });
        var service = new ProjectService(uow, null!, new FakeClaimService(1, Role.PM));

        var response = await service.CalculateMRPForProjectAsync(1, 1);

        Assert.True(response.IsSuccess);
        var item = Assert.Single(Assert.IsType<List<MRPCalculationResponse>>(response.Result));
        Assert.Equal(1, item.WarehouseId);
        Assert.Equal(5, item.CurrentInventory);
        Assert.Equal(15, item.NetQuantityRequired);
    }

    [Fact]
    public async Task MrpTreatsProjectReservationAsSecuredSupply()
    {
        var uow = CreateMrpUnitOfWork(grossRequired: 20, onHand: 20, reserved: 10);
        var request = new MaterialRequest { RequestId = 1, ProjectId = 1, Status = MaterialRequestStatuses.Approved };
        var requestItem = new MaterialRequisition { ItemId = 1, RequestId = 1, MaterialRequest = request, VariantId = 1 };
        uow.RequestRecords.Add(request);
        uow.RequisitionRecords.Add(requestItem);
        uow.ReservationRecords.Add(new InventoryReservation
        {
            ReservationId = 1,
            InventoryId = 1,
            InventoryRecord = uow.InventoryRecords.Single(),
            RequestId = 1,
            MaterialRequest = request,
            RequestItemId = 1,
            RequestItem = requestItem,
            Quantity = 10,
            Status = InventoryReservationStatuses.Active
        });

        var response = await new ProjectService(uow, null!, new FakeClaimService(1, Role.PM))
            .CalculateMRPForProjectAsync(1, 1);

        Assert.True(response.IsSuccess);
        var item = Assert.Single(Assert.IsType<List<MRPCalculationResponse>>(response.Result));
        Assert.Equal(0, item.NetQuantityRequired);
    }

    [Fact]
    public async Task MrpCountsOnlyThisProjectsOpenOrders()
    {
        var uow = CreateMrpUnitOfWork(grossRequired: 20, onHand: 0, reserved: 0);
        var ownPo = new PurchaseOrder { PoId = 1, ProjectId = 1, WarehouseId = 1, Status = PurchaseOrderStatus.APPROVED };
        var otherPo = new PurchaseOrder { PoId = 2, ProjectId = 2, WarehouseId = 1, Status = PurchaseOrderStatus.APPROVED };
        uow.PurchaseOrderRecords.AddRange(new[] { ownPo, otherPo });
        uow.OrderLineRecords.AddRange(new[]
        {
            new OrderLineItem { LineItemId = 1, PurchaseOrder = ownPo, PoId = 1, VariantId = 1, Quantity = 12 },
            new OrderLineItem { LineItemId = 2, PurchaseOrder = otherPo, PoId = 2, VariantId = 1, Quantity = 100 }
        });

        var response = await new ProjectService(uow, null!, new FakeClaimService(1, Role.PM))
            .CalculateMRPForProjectAsync(1, 1);

        Assert.True(response.IsSuccess);
        var item = Assert.Single(Assert.IsType<List<MRPCalculationResponse>>(response.Result));
        Assert.Equal(12, item.OnOrderQuantity);
        Assert.Equal(8, item.NetQuantityRequired);
    }

    [Fact]
    public async Task MrpDoesNotTreatPendingPurchaseOrdersAsIncomingSupply()
    {
        var uow = CreateMrpUnitOfWork(grossRequired: 20, onHand: 0, reserved: 0);
        var pendingPo = new PurchaseOrder { PoId = 1, ProjectId = 1, WarehouseId = 1, Status = PurchaseOrderStatus.PENDING };
        uow.PurchaseOrderRecords.Add(pendingPo);
        uow.OrderLineRecords.Add(new OrderLineItem
        {
            LineItemId = 1,
            PurchaseOrder = pendingPo,
            PoId = 1,
            VariantId = 1,
            Quantity = 12
        });

        var response = await new ProjectService(uow, null!, new FakeClaimService(1, Role.PM))
            .CalculateMRPForProjectAsync(1, 1);

        var item = Assert.Single(Assert.IsType<List<MRPCalculationResponse>>(response.Result));
        Assert.Equal(0, item.OnOrderQuantity);
        Assert.Equal(20, item.NetQuantityRequired);
    }

    [Fact]
    public async Task MrpDoesNotApplyCompletedTaskIssuesToIncompleteTaskDemand()
    {
        var uow = CreateMrpUnitOfWork(grossRequired: 20, onHand: 0, reserved: 0);
        var completedRequest = new MaterialRequest
        {
            RequestId = 1,
            ProjectId = 1,
            TaskId = 2,
            Status = MaterialRequestStatuses.Issued
        };
        uow.RequestRecords.Add(completedRequest);
        uow.RequisitionRecords.Add(new MaterialRequisition
        {
            ItemId = 1,
            RequestId = 1,
            MaterialRequest = completedRequest,
            VariantId = 1,
            IssuedQuantity = 20,
            Quantity = 20,
            ApprovedQuantity = 20
        });

        var response = await new ProjectService(uow, null!, new FakeClaimService(1, Role.PM))
            .CalculateMRPForProjectAsync(1, 1);

        var item = Assert.Single(Assert.IsType<List<MRPCalculationResponse>>(response.Result));
        Assert.Equal(0, item.IssuedToProjectTasks);
        Assert.Equal(20, item.NetQuantityRequired);
    }

    private static TestUnitOfWork CreateMrpUnitOfWork(decimal grossRequired, decimal onHand, decimal reserved)
    {
        var uow = new TestUnitOfWork();
        uow.ProjectRecords.Add(new Project { ProjectId = 1, ProjectName = "P", PMUserID = 1 });
        uow.WarehouseRecords.Add(new Warehouse { WarehouseId = 1, WarehouseName = "W1", ManagerId = 10 });
        var material = new Material { MaterialId = 1, MaterialName = "Steel", DefaultUnit = "kg" };
        var variant = new MaterialVariant { VariantId = 1, MaterialId = 1, Material = material, VariantName = "Standard", Unit = "kg", IsActive = true };
        var task = new TaskItem { TaskId = 1, ProjectId = 1, TaskName = "T", PhaseName = "P", BaselineStart = DateTime.UtcNow, ActualProgressPct = 0 };
        uow.RequirementRecords.Add(new TaskMaterialRequirement { Id = 1, TaskId = 1, TaskItem = task, VariantId = 1, Variant = variant, GrossQuantityRequired = grossRequired });
        uow.InventoryRecords.Add(new InventoryRecord { InventoryId = 1, WarehouseId = 1, VariantId = 1, QuantityOnHand = onHand, ReservedQuantity = reserved });
        return uow;
    }

    private static (WarehouseTransferService Service, TestUnitOfWork Uow) CreateTransferService(
        int managerId, string status = WarehouseTransferStatuses.Requested, decimal requested = 5, decimal shipped = 0)
    {
        var uow = new TestUnitOfWork();
        var source = new Warehouse { WarehouseId = 1, WarehouseName = "Source", ManagerId = 10 };
        var destination = new Warehouse { WarehouseId = 2, WarehouseName = "Destination", ManagerId = 20 };
        var material = new Material { MaterialId = 1, MaterialName = "Steel", DefaultUnit = "kg" };
        var variant = new MaterialVariant { VariantId = 1, MaterialId = 1, Material = material, VariantName = "Standard", Unit = "kg", IsActive = true };
        var item = new WarehouseTransferItem
        {
            TransferItemId = 1,
            TransferId = 1,
            VariantId = 1,
            Variant = variant,
            RequestedQuantity = requested,
            ShippedQuantity = shipped
        };
        var transfer = new WarehouseTransfer
        {
            TransferId = 1,
            SourceWarehouseId = 1,
            SourceWarehouse = source,
            DestinationWarehouseId = 2,
            DestinationWarehouse = destination,
            RequestedByUserId = 10,
            RequestedAt = DateTime.UtcNow,
            Status = status,
            Items = new List<WarehouseTransferItem> { item }
        };
        item.Transfer = transfer;
        uow.WarehouseRecords.AddRange(new[] { source, destination });
        uow.VariantRecords.Add(variant);
        uow.TransferRecords.Add(transfer);
        uow.TransferItemRecords.Add(item);
        return (new WarehouseTransferService(uow, new FakeClaimService(managerId, Role.WAREHOUSE_MANAGER)), uow);
    }
}

internal sealed class FakeClaimService : IClaimService
{
    private readonly ClaimDTO _claim;
    public FakeClaimService(int id, Role role) => _claim = new ClaimDTO { Id = id, Role = role.ToString(), Name = "Test" };
    public ClaimDTO GetUserClaim() => _claim;
}

internal class FakeRepository<T> : IGenericRepository<T> where T : class
{
    protected readonly List<T> Data;
    public FakeRepository(List<T> data) => Data = data;
    public Task AddAsync(T entity) { AssignIdentity(entity); Data.Add(entity); return Task.CompletedTask; }
    public Task AddRangeAsync(List<T> entities) { foreach (var entity in entities) { AssignIdentity(entity); Data.Add(entity); } return Task.CompletedTask; }
    public Task<int> CountAsync() => Task.FromResult(Data.Count);
    public Task<List<T>> GetAllAsync(Expression<Func<T, bool>>? filter)
    {
        IEnumerable<T> query = Data;
        if (filter != null) query = Data.AsQueryable().Where(filter);
        return Task.FromResult(query.ToList());
    }
    public Task<List<T>> GetAllAsync(Expression<Func<T, bool>>? filter, Func<IQueryable<T>, IIncludableQueryable<T, object>>? include = null, int pageIndex = 1, int pageSize = 25) => GetAllAsync(filter);
    public Task<List<T>> GetAllIgnoringQueryFiltersAsync(Expression<Func<T, bool>>? filter) => GetAllAsync(filter);
    public Task<T> GetAsync(Expression<Func<T, bool>> filter) => Task.FromResult(Data.AsQueryable().FirstOrDefault(filter)!);
    public Task<T> GetAsync(Expression<Func<T, bool>> filter, Func<IQueryable<T>, IIncludableQueryable<T, object>>? include) => GetAsync(filter);
    public Task<T?> GetByIdAsync(object id)
    {
        var key = KeyProperty();
        return Task.FromResult(Data.FirstOrDefault(x => Equals(key?.GetValue(x), id)));
    }
    public void Remove(T entity) => Data.Remove(entity);
    public async Task RemoveByIdAsync(object id) { var entity = await GetByIdAsync(id); if (entity != null) Data.Remove(entity); }
    public void Update(T entity) { }

    private static PropertyInfo? KeyProperty() => typeof(T).GetProperties().FirstOrDefault(p =>
        p.Name is "Id" or "WarehouseId" or "VariantId" or "InventoryId" or "TransferId" or "TransferItemId" or
        "ProjectId" or "MaterialId" or "ItemId" or "TransactionId" or "TaskId" or "PoId" or "LineItemId" or "RequestId" or
        "SupplierId" or "CatalogId" or "MetricId" or "ReportId" or "ReservationId" or "AdjustmentId" or "ReturnId" or "TransferReservationId");
    private void AssignIdentity(T entity)
    {
        var key = KeyProperty();
        if (key?.CanWrite != true || key.PropertyType != typeof(int) || (int)key.GetValue(entity)! != 0) return;
        var next = Data.Select(x => (int)key.GetValue(x)!).DefaultIfEmpty().Max() + 1;
        key.SetValue(entity, next);
    }
}

internal sealed class FakeWarehouseRepository : FakeRepository<Warehouse>, IWarehouseRepository { public FakeWarehouseRepository(List<Warehouse> data) : base(data) { } }
internal sealed class FakeInventoryRepository : FakeRepository<InventoryRecord>, IInventoryRepository { public FakeInventoryRepository(List<InventoryRecord> data) : base(data) { } }
internal sealed class FakeProjectRepository : FakeRepository<Project>, IProjectRepository { public FakeProjectRepository(List<Project> data) : base(data) { } }
internal sealed class FakeRequirementRepository : FakeRepository<TaskMaterialRequirement>, ITaskMaterialRequirementRepository { public FakeRequirementRepository(List<TaskMaterialRequirement> data) : base(data) { } }
internal sealed class FakeRequisitionRepository : FakeRepository<MaterialRequisition>, IMaterialRequisitionRepository { public FakeRequisitionRepository(List<MaterialRequisition> data) : base(data) { } }
internal sealed class FakeTransferRepository : FakeRepository<WarehouseTransfer>, IWarehouseTransferRepository { public FakeTransferRepository(List<WarehouseTransfer> data) : base(data) { } }
internal sealed class FakeTransferItemRepository : FakeRepository<WarehouseTransferItem>, IWarehouseTransferItemRepository { public FakeTransferItemRepository(List<WarehouseTransferItem> data) : base(data) { } }
internal sealed class FakeMaterialRequestRepository : FakeRepository<MaterialRequest>, IMaterialRequestRepository { public FakeMaterialRequestRepository(List<MaterialRequest> data) : base(data) { } }
internal sealed class FakePurchaseOrderRepository : FakeRepository<PurchaseOrder>, IPurchaseOrderRepository
{
    public FakePurchaseOrderRepository(List<PurchaseOrder> data) : base(data) { }
    public Task<PurchaseOrder?> GetWithItemsAsync(int poId) => Task.FromResult(Data.FirstOrDefault(x => x.PoId == poId));
}
internal sealed class FakeOrderLineRepository : FakeRepository<OrderLineItem>, IOrderLineItemRepository { public FakeOrderLineRepository(List<OrderLineItem> data) : base(data) { } }
internal sealed class FakeUserAccountRepository : FakeRepository<UserAccount>, IUserAccountRepository { public FakeUserAccountRepository(List<UserAccount> data) : base(data) { } }
internal sealed class FakeEmailVerificationRepository : FakeRepository<EmailVerification>, IEmailVerificationRepository { public FakeEmailVerificationRepository(List<EmailVerification> data) : base(data) { } }
internal sealed class FakeTaskItemRepository : FakeRepository<TaskItem>, ITaskItemRepository { public FakeTaskItemRepository(List<TaskItem> data) : base(data) { } }
internal sealed class FakeProgressReportRepository : FakeRepository<ProgressReport>, IProgressReportRepository { public FakeProgressReportRepository(List<ProgressReport> data) : base(data) { } }
internal sealed class FakeSupplierRepository : FakeRepository<Supplier>, ISupplierRepository { public FakeSupplierRepository(List<Supplier> data) : base(data) { } }
internal sealed class FakeSupplierCatalogRepository : FakeRepository<SupplierCatalog>, ISupplierCatalogRepository { public FakeSupplierCatalogRepository(List<SupplierCatalog> data) : base(data) { } }
internal sealed class FakeSupplierMetricRepository : FakeRepository<SupplierMetric>, ISupplierMetricRepository { public FakeSupplierMetricRepository(List<SupplierMetric> data) : base(data) { } }
internal sealed class FakeMaterialRepository : FakeRepository<Material>, IMaterialRepository { public FakeMaterialRepository(List<Material> data) : base(data) { } }
internal sealed class FakeRefreshTokenRepository : FakeRepository<RefreshToken>, IRefreshTokenRepository { public FakeRefreshTokenRepository(List<RefreshToken> data) : base(data) { } }

internal sealed class TestUnitOfWork : IUnitOfWork
{
    public void Dispose() { }
    public List<Warehouse> WarehouseRecords { get; } = new();
    public List<InventoryRecord> InventoryRecords { get; } = new();
    public List<InventoryTransaction> TransactionRecords { get; } = new();
    public List<InventoryAdjustment> AdjustmentRecords { get; } = new();
    public List<MaterialVariant> VariantRecords { get; } = new();
    public List<Material> MaterialRecords { get; } = new();
    public List<WarehouseTransfer> TransferRecords { get; } = new();
    public List<WarehouseTransferItem> TransferItemRecords { get; } = new();
    public List<TransferInventoryReservation> TransferReservationRecords { get; } = new();
    public List<Project> ProjectRecords { get; } = new();
    public List<TaskMaterialRequirement> RequirementRecords { get; } = new();
    public List<MaterialRequisition> RequisitionRecords { get; } = new();
    public List<MaterialReturn> MaterialReturnRecords { get; } = new();
    public List<MaterialRequest> RequestRecords { get; } = new();
    public List<InventoryReservation> ReservationRecords { get; } = new();
    public List<PurchaseOrder> PurchaseOrderRecords { get; } = new();
    public List<OrderLineItem> OrderLineRecords { get; } = new();
    public List<UserAccount> UserAccountRecords { get; } = new();
    public List<EmailVerification> EmailVerificationRecords { get; } = new();
    public List<EmailOutboxMessage> EmailOutboxRecords { get; } = new();
    public List<RefreshToken> RefreshTokenRecords { get; } = new();
    public List<TaskItem> TaskRecords { get; } = new();
    public List<ProgressReport> ProgressReportRecords { get; } = new();
    public List<Supplier> SupplierRecords { get; } = new();
    public List<SupplierCatalog> SupplierCatalogRecords { get; } = new();
    public List<SupplierMetric> SupplierMetricRecords { get; } = new();
    public List<MrpPlanningRun> MrpPlanningRunRecords { get; } = new();
    public List<PhysicalCountSession> PhysicalCountSessionRecords { get; } = new();
    public List<PhysicalCountLine> PhysicalCountLineRecords { get; } = new();

    public IWarehouseRepository Warehouses { get; }
    public IInventoryRepository Inventories { get; }
    public IGenericRepository<InventoryTransaction> InventoryTransactions { get; }
    public IGenericRepository<InventoryAdjustment> InventoryAdjustments { get; }
    public IGenericRepository<MaterialVariant> MaterialVariants { get; }
    public IWarehouseTransferRepository WarehouseTransfers { get; }
    public IWarehouseTransferItemRepository WarehouseTransferItems { get; }
    public IGenericRepository<TransferInventoryReservation> TransferInventoryReservations { get; }
    public IProjectRepository Projects { get; }
    public ITaskMaterialRequirementRepository TaskMaterialRequirements { get; }
    public IMaterialRequisitionRepository MaterialRequisitions { get; }
    public IGenericRepository<MaterialReturn> MaterialReturns { get; }

    public TestUnitOfWork()
    {
        Warehouses = new FakeWarehouseRepository(WarehouseRecords);
        Inventories = new FakeInventoryRepository(InventoryRecords);
        InventoryTransactions = new FakeRepository<InventoryTransaction>(TransactionRecords);
        InventoryAdjustments = new FakeRepository<InventoryAdjustment>(AdjustmentRecords);
        MaterialVariants = new FakeRepository<MaterialVariant>(VariantRecords);
        Materials = new FakeMaterialRepository(MaterialRecords);
        WarehouseTransfers = new FakeTransferRepository(TransferRecords);
        WarehouseTransferItems = new FakeTransferItemRepository(TransferItemRecords);
        TransferInventoryReservations = new FakeRepository<TransferInventoryReservation>(TransferReservationRecords);
        Projects = new FakeProjectRepository(ProjectRecords);
        TaskMaterialRequirements = new FakeRequirementRepository(RequirementRecords);
        MaterialRequisitions = new FakeRequisitionRepository(RequisitionRecords);
        MaterialReturns = new FakeRepository<MaterialReturn>(MaterialReturnRecords);
        MaterialRequests = new FakeMaterialRequestRepository(RequestRecords);
        InventoryReservations = new FakeRepository<InventoryReservation>(ReservationRecords);
        PurchaseOrders = new FakePurchaseOrderRepository(PurchaseOrderRecords);
        OrderLineItems = new FakeOrderLineRepository(OrderLineRecords);
        UserAccounts = new FakeUserAccountRepository(UserAccountRecords);
        EmailVerifications = new FakeEmailVerificationRepository(EmailVerificationRecords);
        EmailOutboxMessages = new FakeRepository<EmailOutboxMessage>(EmailOutboxRecords);
        RefreshTokens = new FakeRefreshTokenRepository(RefreshTokenRecords);
        TaskItems = new FakeTaskItemRepository(TaskRecords);
        ProgressReports = new FakeProgressReportRepository(ProgressReportRecords);
        Suppliers = new FakeSupplierRepository(SupplierRecords);
        SupplierCatalogs = new FakeSupplierCatalogRepository(SupplierCatalogRecords);
        SupplierMetrics = new FakeSupplierMetricRepository(SupplierMetricRecords);
        MrpPlanningRuns = new FakeRepository<MrpPlanningRun>(MrpPlanningRunRecords);
        PhysicalCountSessions = new FakeRepository<PhysicalCountSession>(PhysicalCountSessionRecords);
        PhysicalCountLines = new FakeRepository<PhysicalCountLine>(PhysicalCountLineRecords);
    }

    public IUserAccountRepository UserAccounts { get; }
    public IRefreshTokenRepository RefreshTokens { get; }
    public IEmailVerificationRepository EmailVerifications { get; }
    public IGenericRepository<EmailOutboxMessage> EmailOutboxMessages { get; }
    public ITaskItemRepository TaskItems { get; }
    public IProgressReportRepository ProgressReports { get; }
    public IMaterialRepository Materials { get; }
    public ISupplierRepository Suppliers { get; }
    public ISupplierCatalogRepository SupplierCatalogs { get; }
    public ISupplierMetricRepository SupplierMetrics { get; }
    public ICategoryRepository Categories => null!;
    public IMaterialRequestRepository MaterialRequests { get; }
    public IProjectBudgetHistoryRepository ProjectBudgetHistories => null!;
    public IGenericRepository<MrpPlanningRun> MrpPlanningRuns { get; }
    public IGenericRepository<PhysicalCountSession> PhysicalCountSessions { get; }
    public IGenericRepository<PhysicalCountLine> PhysicalCountLines { get; }
    public IChatConversationRepository ChatConversations => null!;
    public IChatParticipantRepository ChatParticipants => null!;
    public IChatMessageRepository ChatMessages => null!;
    public IMeetingRepository Meetings => null!;
    public IMeetingParticipantRepository MeetingParticipants => null!;
    public IPurchaseOrderRepository PurchaseOrders { get; }
    public IOrderLineItemRepository OrderLineItems { get; }
    public IGenericRepository<InventoryReservation> InventoryReservations { get; }
    public Task SaveChangeAsync() => Task.CompletedTask;
    public Task BeginTransactionAsync() => Task.CompletedTask;
    public Task BeginTransactionAsync(IsolationLevel isolationLevel) => Task.CompletedTask;
    public Task CommitTransactionAsync() => Task.CompletedTask;
    public Task RollbackTransactionAsync() => Task.CompletedTask;
    public Task<T> ExecuteScalarAsync<T>(string sql) => throw new NotSupportedException();
    public Task ExecuteRawSqlAsync(string sql) => throw new NotSupportedException();
}
