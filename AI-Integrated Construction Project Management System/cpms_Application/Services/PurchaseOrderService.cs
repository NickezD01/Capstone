using AutoMapper;
using cpms_Application.Interfaces;
using cpms_Application.Request.PurchaseOrder;
using cpms_Application.Request.Warehouse;
using cpms_Application.Response;
using cpms_Application.Response.PurchaseOrder;
using cpms_Domain.Models;
using cpms_Domain;
using Microsoft.EntityFrameworkCore;

namespace cpms_Application.Services
{
    public class PurchaseOrderService : IPurchaseOrderService
    {
        private readonly IUnitOfWork _uow;
        private readonly IMapper _mapper;
        private readonly IClaimService _claimService;

        public PurchaseOrderService(IUnitOfWork uow, IMapper mapper, IClaimService claimService)
        {
            _uow = uow;
            _mapper = mapper;
            _claimService = claimService;
        }

        private async Task<PurchaseOrder?> GetDetailsAsync(int poId) => await _uow.PurchaseOrders.GetAsync(p => p.PoId == poId,
            q => q.Include(p => p.Project).Include(p => p.Supplier).Include(p => p.Warehouse)
                  .Include(p => p.OrderLineItems).ThenInclude(l => l.Variant).ThenInclude(v => v.Material));

        public async Task<ApiResponse> CreatePurchaseOrderAsync(CreatePurchaseOrderRequest request)
        {
            var user = _claimService.GetUserClaim();
            if (!IsWarehouseManager(user)) return Forbidden("Only warehouse managers may create purchase orders.");
            if (request.Items == null || request.Items.Count == 0) return new ApiResponse().SetBadRequest(message: "At least one order item is required.");
            if (request.ExpectedDeliveryDate.HasValue && request.ExpectedDeliveryDate.Value.Date < DateTime.UtcNow.Date)
                return new ApiResponse().SetBadRequest(message: "ExpectedDeliveryDate cannot be in the past.");
            var linkedRequestItemIds = request.Items.Where(x => x.RequestItemId.HasValue).Select(x => x.RequestItemId!.Value).ToList();
            if (linkedRequestItemIds.Distinct().Count() != linkedRequestItemIds.Count)
                return new ApiResponse().SetBadRequest(message: "A material request item may only appear once per purchase order.");
            await _uow.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                async Task<ApiResponse> Abort(ApiResponse response)
                {
                    await _uow.RollbackTransactionAsync();
                    return response;
                }

                var project = await _uow.Projects.GetAsync(p => p.ProjectId == request.ProjectId);
                var warehouse = await _uow.Warehouses.GetAsync(w => w.WarehouseId == request.WarehouseId);
                var supplier = await _uow.Suppliers.GetAsync(s => s.SupplierId == request.SupplierId);
                if (project == null || supplier == null || warehouse == null)
                    return await Abort(new ApiResponse().SetBadRequest(message: "Project, supplier, or warehouse does not exist."));
                if (project.Status is ProjectStatus.COMPLETED or ProjectStatus.CANCELLED)
                    return await Abort(new ApiResponse().SetConflict(message: "Closed projects cannot accept purchase orders."));
                if (warehouse.ManagerId != user.Id)
                    return await Abort(Forbidden("You may only create purchase orders for a warehouse you manage."));

                var resolved = new List<(OrderLineItemDto Item, int VariantId, decimal UnitPrice)>();
                foreach (var item in request.Items)
                {
                    if (item.Quantity <= 0 || item.UnitPrice < 0)
                        return await Abort(new ApiResponse().SetBadRequest(message: "Order quantities must be positive and prices cannot be negative."));
                    var variant = item.VariantId != 0
                        ? await _uow.MaterialVariants.GetAsync(v => v.VariantId == item.VariantId)
                        : await _uow.MaterialVariants.GetAsync(v => v.MaterialId == item.MaterialId && v.IsActive);
                    if (variant == null || !variant.IsActive)
                        return await Abort(new ApiResponse().SetBadRequest(message: "Material variant not found or inactive."));
                    var catalog = await _uow.SupplierCatalogs.GetAsync(c =>
                        c.SupplierId == request.SupplierId && c.VariantId == variant.VariantId && c.IsAvailable);
                    if (catalog == null)
                        return await Abort(new ApiResponse().SetBadRequest(message: $"Variant {variant.VariantId} is not available from the selected supplier."));
                    if (item.Quantity < catalog.MinimumOrderQuantity)
                        return await Abort(new ApiResponse().SetBadRequest(message: $"Variant {variant.VariantId} is below the supplier minimum order quantity."));
                    if (request.ExpectedDeliveryDate.HasValue &&
                        request.ExpectedDeliveryDate.Value.Date < DateTime.UtcNow.Date.AddDays(catalog.LeadTimeDays))
                        return await Abort(new ApiResponse().SetBadRequest(message: $"Expected delivery for variant {variant.VariantId} violates the supplier lead time of {catalog.LeadTimeDays} days."));
                    if (item.UnitPrice > 0 && item.UnitPrice != catalog.UnitPrice)
                        return await Abort(new ApiResponse().SetConflict(message: $"Submitted price for variant {variant.VariantId} differs from the authoritative catalog price {catalog.UnitPrice}. Refresh the catalog before ordering."));
                    if (item.RequestItemId.HasValue)
                    {
                        var requestItem = await _uow.MaterialRequisitions.GetAsync(r => r.ItemId == item.RequestItemId.Value);
                        if (requestItem == null || requestItem.VariantId != variant.VariantId)
                            return await Abort(new ApiResponse().SetBadRequest(message: "RequestItemId does not match the ordered variant."));
                        var materialRequest = await _uow.MaterialRequests.GetAsync(r => r.RequestId == requestItem.RequestId);
                        if (materialRequest == null || materialRequest.ProjectId != request.ProjectId ||
                            materialRequest.Status is not (MaterialRequestStatuses.Approved or MaterialRequestStatuses.PartiallyApproved or MaterialRequestStatuses.Issued or MaterialRequestStatuses.PartiallyIssued))
                            return await Abort(new ApiResponse().SetBadRequest(message: "RequestItemId must belong to an approved material request for this project."));
                        var existingLines = await _uow.OrderLineItems.GetAllAsync(l =>
                            l.RequestItemId == requestItem.ItemId &&
                            l.PurchaseOrder.Status != PurchaseOrderStatus.REJECTED &&
                            l.PurchaseOrder.Status != PurchaseOrderStatus.CANCELLED);
                        var remainingShortage = requestItem.Quantity - requestItem.ApprovedQuantity - existingLines.Sum(l => l.Quantity);
                        if (item.Quantity > remainingShortage)
                            return await Abort(new ApiResponse().SetConflict(message: "Ordered quantity exceeds the remaining request shortage."));
                    }
                    resolved.Add((item, variant.VariantId, catalog.UnitPrice));
                }
                if (resolved.GroupBy(x => x.VariantId).Any(g => g.Count() > 1))
                    return await Abort(new ApiResponse().SetBadRequest(message: "A material variant may only appear once per purchase order."));

                var total = resolved.Sum(x => x.Item.Quantity * x.UnitPrice);
                if (project.TotalProjectBudget > 0)
                {
                    var committed = await _uow.PurchaseOrders.GetAllAsync(p => p.ProjectId == request.ProjectId &&
                        p.Status != PurchaseOrderStatus.REJECTED && p.Status != PurchaseOrderStatus.CANCELLED);
                    if (committed.Sum(p => p.TotalAmount) + total > project.TotalProjectBudget)
                        return await Abort(new ApiResponse().SetConflict(message: "Purchase order exceeds the remaining project budget."));
                }

                var po = new PurchaseOrder
                {
                    ProjectId = request.ProjectId,
                    SupplierId = request.SupplierId,
                    WarehouseId = request.WarehouseId,
                    UserAccountId = user.Id,
                    TotalAmount = total,
                    OrderDate = DateTime.UtcNow,
                    ExpectedDeliveryDate = request.ExpectedDeliveryDate,
                    Note = request.Note,
                    Status = PurchaseOrderStatus.PENDING
                };
                foreach (var entry in resolved)
                    po.OrderLineItems.Add(new OrderLineItem
                    {
                        VariantId = entry.VariantId,
                        RequestItemId = entry.Item.RequestItemId,
                        Quantity = entry.Item.Quantity,
                        UnitPrice = entry.UnitPrice
                    });
                await _uow.PurchaseOrders.AddAsync(po);
                await _uow.SaveChangeAsync();
                await _uow.CommitTransactionAsync();
                return new ApiResponse().SetApiResponse(System.Net.HttpStatusCode.Created, true,
                    result: _mapper.Map<PurchaseOrderResponse>(await GetDetailsAsync(po.PoId)));
            }
            catch (DbUpdateException)
            {
                await _uow.RollbackTransactionAsync();
                return new ApiResponse().SetConflict(message: "Purchase order data changed while it was being created. Reload and retry.");
            }
            catch (Exception)
            {
                await _uow.RollbackTransactionAsync();
                return new ApiResponse().SetApiResponse(System.Net.HttpStatusCode.InternalServerError, false, "Unable to create purchase order.");
            }
        }

        public async Task<ApiResponse> GetAllPurchaseOrdersAsync()
        {
            var user = _claimService.GetUserClaim();
            System.Linq.Expressions.Expression<Func<PurchaseOrder, bool>>? accessFilter = user.Role.ToUpperInvariant() switch
            {
                nameof(Role.ADMIN) => null,
                nameof(Role.PM) => p => p.Project.PMUserID == user.Id,
                nameof(Role.WAREHOUSE_MANAGER) => p => p.Warehouse.ManagerId == user.Id,
                _ => p => false
            };
            var pos = await _uow.PurchaseOrders.GetAllAsync(accessFilter,
                q => q.Include(p => p.Project).Include(p => p.Supplier).Include(p => p.Warehouse)
                      .Include(p => p.OrderLineItems).ThenInclude(l => l.Variant).ThenInclude(v => v.Material));
            return new ApiResponse().SetOk(_mapper.Map<List<PurchaseOrderResponse>>(pos));
        }

        public async Task<ApiResponse> ApprovePurchaseOrderAsync(int poId)
        {
            var user = _claimService.GetUserClaim();
            if (!IsPurchaseOrderApproverRole(user)) return Forbidden("Only administrators or project managers may approve purchase orders.");
            await _uow.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                var po = await _uow.PurchaseOrders.GetAsync(p => p.PoId == poId,
                    q => q.Include(p => p.Project).Include(p => p.Warehouse).Include(p => p.OrderLineItems));
                if (po == null) { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetNotFound(message: "Purchase order not found."); }
                if (!CanApprovePurchaseOrder(user, po)) { await _uow.RollbackTransactionAsync(); return Forbidden("You may only approve purchase orders for a project you manage."); }
                if (po.UserAccountId == user.Id) { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetConflict(message: "The purchase-order creator cannot approve the same order."); }
                if (po.Status != PurchaseOrderStatus.PENDING) { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetConflict(message: "Only pending purchase orders can be approved."); }

                foreach (var line in po.OrderLineItems)
                {
                    var inventory = await _uow.Inventories.GetAsync(i => i.WarehouseId == po.WarehouseId && i.VariantId == line.VariantId);
                    if (inventory == null)
                    {
                        inventory = new InventoryRecord { WarehouseId = po.WarehouseId, VariantId = line.VariantId, UpdatedAt = DateTime.UtcNow };
                        await _uow.Inventories.AddAsync(inventory);
                    }
                    inventory.OnOrderQuantity += line.Quantity - line.ReceivedQuantity;
                    inventory.UpdatedAt = DateTime.UtcNow;
                }
                po.Status = PurchaseOrderStatus.APPROVED;
                po.ApprovedByUserId = user.Id;
                po.ApprovedAt = DateTime.UtcNow;
                await _uow.SaveChangeAsync();
                await _uow.CommitTransactionAsync();
                return new ApiResponse().SetOk(_mapper.Map<PurchaseOrderResponse>(await GetDetailsAsync(poId)));
            }
            catch (DbUpdateConcurrencyException)
            {
                await _uow.RollbackTransactionAsync();
                return new ApiResponse().SetConflict(message: "Inventory changed while approving the purchase order. Reload and retry.");
            }
            catch (Exception)
            {
                await _uow.RollbackTransactionAsync();
                return new ApiResponse().SetApiResponse(System.Net.HttpStatusCode.InternalServerError, false, "Unable to approve purchase order.");
            }
        }

        public async Task<ApiResponse> RejectPurchaseOrderAsync(int poId)
        {
            var user = _claimService.GetUserClaim();
            if (!IsPurchaseOrderApproverRole(user)) return Forbidden("Only administrators or project managers may reject purchase orders.");
            var po = await _uow.PurchaseOrders.GetAsync(p => p.PoId == poId, q => q.Include(p => p.Project));
            if (po == null) return new ApiResponse().SetNotFound(message: "Purchase order not found.");
            if (!CanApprovePurchaseOrder(user, po)) return Forbidden("You may only reject purchase orders for a project you manage.");
            if (po.UserAccountId == user.Id) return new ApiResponse().SetConflict(message: "The purchase-order creator cannot reject the same order.");
            if (po.Status != PurchaseOrderStatus.PENDING) return new ApiResponse().SetConflict(message: "Only pending purchase orders can be rejected.");
            po.Status = PurchaseOrderStatus.REJECTED;
            await _uow.SaveChangeAsync();
            return new ApiResponse().SetOk(_mapper.Map<PurchaseOrderResponse>(await GetDetailsAsync(poId)));
        }

        public async Task<ApiResponse> ReceivePurchaseOrderAsync(int poId, ReceivePurchaseOrderRequest request)
        {
            var user = _claimService.GetUserClaim();
            if (!IsWarehouseManager(user)) return Forbidden("Only warehouse managers may receive purchase orders.");
            if (request.Items == null || request.Items.Count == 0 || request.Items.Any(i =>
                    i.Quantity < 0 || i.DamagedQuantity < 0 || i.MissingQuantity < 0 ||
                    i.Quantity + i.DamagedQuantity + i.MissingQuantity <= 0))
                return new ApiResponse().SetBadRequest(message: "Every receipt line must account for a positive accepted, damaged, or missing quantity.");
            if (!request.IsFinalDelivery && request.Items.Any(i => i.MissingQuantity > 0))
                return new ApiResponse().SetBadRequest(message: "Missing quantities may only be declared on a final delivery.");
            await _uow.BeginTransactionAsync();
            try
            {
                var po = await _uow.PurchaseOrders.GetAsync(p => p.PoId == poId, q => q.Include(p => p.Warehouse).Include(p => p.OrderLineItems));
                if (po == null) { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetNotFound(message: "Purchase order not found."); }
                if (po.Warehouse.ManagerId != user.Id) { await _uow.RollbackTransactionAsync(); return Forbidden("You may only receive purchase orders into a warehouse you manage."); }
                if (po.Status is not (PurchaseOrderStatus.APPROVED or PurchaseOrderStatus.PROCESSING or PurchaseOrderStatus.SHIPPED or PurchaseOrderStatus.PARTIALLY_RECEIVED))
                { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetConflict(message: "This purchase order cannot receive deliveries in its current state."); }
                if (request.Items.Select(i => i.LineItemId).Distinct().Count() != request.Items.Count)
                { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetBadRequest(message: "Receipt contains duplicate line items."); }

                foreach (var receipt in request.Items)
                {
                    var line = po.OrderLineItems.SingleOrDefault(l => l.LineItemId == receipt.LineItemId);
                    var accountedBefore = line == null ? 0 : line.ReceivedQuantity + line.DamagedQuantity + line.MissingQuantity;
                    var deliveredNow = receipt.Quantity + receipt.DamagedQuantity + receipt.MissingQuantity;
                    if (line == null || accountedBefore + deliveredNow > line.Quantity)
                    { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetBadRequest(message: "Receipt quantity exceeds an order line's remaining quantity."); }
                    var inventory = await _uow.Inventories.GetAsync(i => i.WarehouseId == po.WarehouseId && i.VariantId == line.VariantId);
                    if (inventory == null)
                    {
                        inventory = new InventoryRecord { WarehouseId = po.WarehouseId, VariantId = line.VariantId, UpdatedAt = DateTime.UtcNow };
                        await _uow.Inventories.AddAsync(inventory);
                        await _uow.SaveChangeAsync();
                    }
                    var before = inventory.QuantityOnHand;
                    if (receipt.Quantity > 0)
                    {
                        var newQuantity = before + receipt.Quantity;
                        inventory.AverageUnitCost = newQuantity == 0 ? 0 :
                            ((before * inventory.AverageUnitCost) + (receipt.Quantity * line.UnitPrice)) / newQuantity;
                    }
                    inventory.QuantityOnHand += receipt.Quantity;
                    inventory.OnOrderQuantity = Math.Max(0, inventory.OnOrderQuantity - receipt.Quantity);
                    inventory.UpdatedAt = DateTime.UtcNow;
                    line.ReceivedQuantity += receipt.Quantity;
                    line.DamagedQuantity += receipt.DamagedQuantity;
                    line.MissingQuantity += receipt.MissingQuantity;
                    inventory.OnOrderQuantity = Math.Max(0, inventory.OnOrderQuantity - receipt.DamagedQuantity - receipt.MissingQuantity);
                    await _uow.InventoryTransactions.AddAsync(new InventoryTransaction
                    {
                        InventoryId = inventory.InventoryId,
                        WarehouseId = po.WarehouseId,
                        VariantId = line.VariantId,
                        TransactionType = InventoryTransactionTypes.Receipt,
                        Quantity = receipt.Quantity,
                        QuantityBefore = before,
                        QuantityAfter = inventory.QuantityOnHand,
                        ReferenceId = poId,
                        ReferenceType = "PURCHASE_ORDER",
                        Note = request.Note,
                        UnitCost = line.UnitPrice,
                        TotalValue = receipt.Quantity * line.UnitPrice,
                        LotNumber = receipt.LotNumber,
                        BatchNumber = receipt.BatchNumber,
                        SerialNumber = receipt.SerialNumber,
                        ExpiryDate = receipt.ExpiryDate,
                        PerformedByUserId = user.Id,
                        TransactionDate = DateTime.UtcNow
                    });
                }
                var fullyAccounted = po.OrderLineItems.All(l => l.ReceivedQuantity + l.DamagedQuantity + l.MissingQuantity == l.Quantity);
                var hasVariance = po.OrderLineItems.Any(l => l.DamagedQuantity > 0 || l.MissingQuantity > 0);
                if (fullyAccounted)
                {
                    po.Status = hasVariance ? PurchaseOrderStatus.CLOSED_WITH_VARIANCE : PurchaseOrderStatus.DELIVERED;
                    await UpdateSupplierMetricsAsync(po);
                }
                else
                    po.Status = PurchaseOrderStatus.PARTIALLY_RECEIVED;
                await _uow.SaveChangeAsync();
                await _uow.CommitTransactionAsync();
                return new ApiResponse().SetOk(_mapper.Map<PurchaseOrderResponse>(await GetDetailsAsync(poId)));
            }
            catch (DbUpdateConcurrencyException)
            {
                await _uow.RollbackTransactionAsync();
                return new ApiResponse().SetConflict(message: "Inventory changed while receiving the purchase order. Reload and retry.");
            }
            catch (DbUpdateException)
            {
                await _uow.RollbackTransactionAsync();
                return new ApiResponse().SetConflict(message: "Receipt or supplier metrics changed concurrently. Reload and retry.");
            }
            catch (Exception)
            {
                await _uow.RollbackTransactionAsync();
                return new ApiResponse().SetApiResponse(System.Net.HttpStatusCode.InternalServerError, false, "Unable to receive purchase order.");
            }
        }

        public async Task<ApiResponse> ImportToWarehouseAsync(int poId, int warehouseId)
        {
            var po = await GetDetailsAsync(poId);
            if (po == null) return new ApiResponse().SetNotFound(message: "Purchase order not found.");
            if (po.WarehouseId != warehouseId) return new ApiResponse().SetBadRequest(message: "The purchase order is allocated to a different warehouse.");
            return await ReceivePurchaseOrderAsync(poId, new ReceivePurchaseOrderRequest
            {
                Items = po.OrderLineItems.Where(l => l.Quantity > l.ReceivedQuantity)
                    .Select(l => new ReceivePurchaseOrderItemRequest { LineItemId = l.LineItemId, Quantity = l.Quantity - l.ReceivedQuantity }).ToList()
            });
        }

        private async Task UpdateSupplierMetricsAsync(PurchaseOrder po)
        {
            var metric = await _uow.SupplierMetrics.GetAsync(m => m.SupplierId == po.SupplierId);
            if (metric == null)
            {
                metric = new SupplierMetric { SupplierId = po.SupplierId, QualityScore = 100 };
                await _uow.SupplierMetrics.AddAsync(metric);
            }

            var oldCount = metric.EvaluatedOrderCount;
            var newCount = oldCount + 1;
            var delayDays = po.ExpectedDeliveryDate.HasValue
                ? Math.Max(0, (DateTime.UtcNow.Date - po.ExpectedDeliveryDate.Value.Date).TotalDays)
                : 0;
            var ordered = po.OrderLineItems.Sum(l => l.Quantity);
            var defective = po.OrderLineItems.Sum(l => l.DamagedQuantity + l.MissingQuantity);
            var orderDefectRate = ordered <= 0 ? 0 : (double)(defective / ordered * 100m);
            var onTime = !po.ExpectedDeliveryDate.HasValue || DateTime.UtcNow.Date <= po.ExpectedDeliveryDate.Value.Date ? 100d : 0d;

            metric.AvgDeliveryDelay = ((metric.AvgDeliveryDelay * oldCount) + delayDays) / newCount;
            metric.DefectRatePct = ((metric.DefectRatePct * oldCount) + orderDefectRate) / newCount;
            metric.OnTimeDeliveryRatePct = ((metric.OnTimeDeliveryRatePct * oldCount) + onTime) / newCount;
            metric.QualityScore = Math.Clamp(100d - metric.DefectRatePct, 0d, 100d);
            metric.ReliabilityScore = Math.Clamp((metric.OnTimeDeliveryRatePct * 0.6d) + (metric.QualityScore * 0.4d), 0d, 100d);
            metric.EvaluatedOrderCount = newCount;
            metric.LastEvaluatedAt = DateTime.UtcNow;
        }

        public async Task<ApiResponse> MarkShippedAsync(int poId)
        {
            var user = _claimService.GetUserClaim();
            if (!IsWarehouseManager(user)) return Forbidden("Only warehouse managers may record shipment state.");
            var po = await _uow.PurchaseOrders.GetAsync(p => p.PoId == poId, q => q.Include(p => p.Warehouse));
            if (po == null) return new ApiResponse().SetNotFound("Purchase order not found.");
            if (po.Warehouse.ManagerId != user.Id) return Forbidden("You do not manage this purchase order's warehouse.");
            if (po.Status is not (PurchaseOrderStatus.APPROVED or PurchaseOrderStatus.PROCESSING))
                return new ApiResponse().SetConflict("Only approved or processing purchase orders can be marked shipped.");
            po.Status = PurchaseOrderStatus.SHIPPED;
            await _uow.SaveChangeAsync();
            return new ApiResponse().SetOk("Purchase order marked as shipped.");
        }

        public async Task<ApiResponse> CancelPurchaseOrderAsync(int poId)
        {
            var user = _claimService.GetUserClaim();
            await _uow.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                var po = await _uow.PurchaseOrders.GetAsync(p => p.PoId == poId,
                    q => q.Include(p => p.Project).Include(p => p.Warehouse).Include(p => p.OrderLineItems));
                if (po == null) { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetNotFound("Purchase order not found."); }
                var managerCanCancel = IsWarehouseManager(user) && po.Warehouse.ManagerId == user.Id && po.Status == PurchaseOrderStatus.PENDING;
                if (!managerCanCancel && !CanApprovePurchaseOrder(user, po))
                { await _uow.RollbackTransactionAsync(); return Forbidden("You cannot cancel this purchase order."); }
                if (po.Status is PurchaseOrderStatus.REJECTED or PurchaseOrderStatus.CANCELLED or PurchaseOrderStatus.DELIVERED or PurchaseOrderStatus.CLOSED_WITH_VARIANCE)
                { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetConflict("This purchase order is already closed."); }
                if (po.OrderLineItems.Any(line => line.ReceivedQuantity > 0 || line.DamagedQuantity > 0 || line.MissingQuantity > 0))
                { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetConflict("A partially received purchase order must be closed through delivery variance processing."); }

                if (po.Status != PurchaseOrderStatus.PENDING)
                {
                    foreach (var line in po.OrderLineItems)
                    {
                        var inventory = await _uow.Inventories.GetAsync(i => i.WarehouseId == po.WarehouseId && i.VariantId == line.VariantId);
                        if (inventory != null)
                        {
                            inventory.OnOrderQuantity = Math.Max(0, inventory.OnOrderQuantity - line.Quantity);
                            inventory.UpdatedAt = DateTime.UtcNow;
                        }
                    }
                }
                po.Status = PurchaseOrderStatus.CANCELLED;
                await _uow.SaveChangeAsync();
                await _uow.CommitTransactionAsync();
                return new ApiResponse().SetOk("Purchase order cancelled.");
            }
            catch
            {
                await _uow.RollbackTransactionAsync();
                throw;
            }
        }

        private static bool IsWarehouseManager(ClaimDTO claim) => string.Equals(claim.Role, Role.WAREHOUSE_MANAGER.ToString(), StringComparison.OrdinalIgnoreCase);
        private static bool IsPurchaseOrderApproverRole(ClaimDTO claim) =>
            string.Equals(claim.Role, Role.ADMIN.ToString(), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(claim.Role, Role.PM.ToString(), StringComparison.OrdinalIgnoreCase);
        private static bool CanApprovePurchaseOrder(ClaimDTO claim, PurchaseOrder order) =>
            string.Equals(claim.Role, Role.ADMIN.ToString(), StringComparison.OrdinalIgnoreCase) ||
            (string.Equals(claim.Role, Role.PM.ToString(), StringComparison.OrdinalIgnoreCase) && order.Project.PMUserID == claim.Id);
        private static ApiResponse Forbidden(string message) => new ApiResponse().SetApiResponse(System.Net.HttpStatusCode.Forbidden, false, message);
    }
}
