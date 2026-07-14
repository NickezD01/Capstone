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
            var project = await _uow.Projects.GetByIdAsync(request.ProjectId);
            if (project == null || await _uow.Suppliers.GetByIdAsync(request.SupplierId) == null || await _uow.Warehouses.GetByIdAsync(request.WarehouseId) == null)
                return new ApiResponse().SetBadRequest(message: "Project, supplier, or warehouse does not exist.");

            var resolved = new List<(OrderLineItemDto Item, int VariantId)>();
            foreach (var item in request.Items)
            {
                if (item.Quantity <= 0 || item.UnitPrice < 0) return new ApiResponse().SetBadRequest(message: "Order quantities must be positive and prices cannot be negative.");
                var variant = item.VariantId != 0 ? await _uow.MaterialVariants.GetByIdAsync(item.VariantId)
                    : await _uow.MaterialVariants.GetAsync(v => v.MaterialId == item.MaterialId && v.IsActive);
                if (variant == null) return new ApiResponse().SetBadRequest(message: "Material variant not found.");
                var catalog = await _uow.SupplierCatalogs.GetAsync(c => c.SupplierId == request.SupplierId && c.VariantId == variant.VariantId && c.IsAvailable);
                if (catalog == null) return new ApiResponse().SetBadRequest(message: $"Variant {variant.VariantId} is not available from the selected supplier.");
                if (item.Quantity < catalog.MinimumOrderQuantity)
                    return new ApiResponse().SetBadRequest(message: $"Variant {variant.VariantId} is below the supplier minimum order quantity.");
                if (item.RequestItemId.HasValue)
                {
                    var requestItem = await _uow.MaterialRequisitions.GetByIdAsync(item.RequestItemId.Value);
                    if (requestItem == null || requestItem.VariantId != variant.VariantId)
                        return new ApiResponse().SetBadRequest(message: "RequestItemId does not match the ordered variant.");
                }
                resolved.Add((item, variant.VariantId));
            }

            var total = resolved.Sum(x => x.Item.Quantity * x.Item.UnitPrice);
            if (project.TotalProjectBudget > 0)
            {
                var committed = await _uow.PurchaseOrders.GetAllAsync(p => p.ProjectId == request.ProjectId && p.Status != PurchaseOrderStatus.REJECTED);
                if (committed.Sum(p => p.TotalAmount) + total > project.TotalProjectBudget)
                    return new ApiResponse().SetConflict(message: "Purchase order exceeds the remaining project budget.");
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
                    UnitPrice = entry.Item.UnitPrice
                });
            await _uow.PurchaseOrders.AddAsync(po);
            await _uow.SaveChangeAsync();
            return new ApiResponse().SetOk(_mapper.Map<PurchaseOrderResponse>(await GetDetailsAsync(po.PoId)));
        }

        public async Task<ApiResponse> GetAllPurchaseOrdersAsync()
        {
            var pos = await _uow.PurchaseOrders.GetAllAsync(null,
                q => q.Include(p => p.Project).Include(p => p.Supplier).Include(p => p.Warehouse)
                      .Include(p => p.OrderLineItems).ThenInclude(l => l.Variant).ThenInclude(v => v.Material));
            return new ApiResponse().SetOk(_mapper.Map<List<PurchaseOrderResponse>>(pos));
        }

        public async Task<ApiResponse> ApprovePurchaseOrderAsync(int poId)
        {
            var user = _claimService.GetUserClaim();
            if (!IsWarehouseManager(user)) return Forbidden("Only warehouse managers may approve purchase orders.");
            await _uow.BeginTransactionAsync();
            try
            {
                var po = await _uow.PurchaseOrders.GetAsync(p => p.PoId == poId, q => q.Include(p => p.OrderLineItems));
                if (po == null) { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetNotFound(message: "Purchase order not found."); }
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
            catch (Exception ex)
            {
                await _uow.RollbackTransactionAsync();
                return new ApiResponse().SetBadRequest(message: "Unable to approve purchase order: " + ex.Message);
            }
        }

        public async Task<ApiResponse> RejectPurchaseOrderAsync(int poId)
        {
            var user = _claimService.GetUserClaim();
            if (!IsWarehouseManager(user)) return Forbidden("Only warehouse managers may reject purchase orders.");
            var po = await _uow.PurchaseOrders.GetByIdAsync(poId);
            if (po == null) return new ApiResponse().SetNotFound(message: "Purchase order not found.");
            if (po.Status != PurchaseOrderStatus.PENDING) return new ApiResponse().SetConflict(message: "Only pending purchase orders can be rejected.");
            po.Status = PurchaseOrderStatus.REJECTED;
            await _uow.SaveChangeAsync();
            return new ApiResponse().SetOk(_mapper.Map<PurchaseOrderResponse>(await GetDetailsAsync(poId)));
        }

        public async Task<ApiResponse> ReceivePurchaseOrderAsync(int poId, ReceivePurchaseOrderRequest request)
        {
            var user = _claimService.GetUserClaim();
            if (!IsWarehouseManager(user)) return Forbidden("Only warehouse managers may receive purchase orders.");
            if (request.Items == null || request.Items.Count == 0 || request.Items.Any(i => i.Quantity <= 0))
                return new ApiResponse().SetBadRequest(message: "At least one positive receipt quantity is required.");
            await _uow.BeginTransactionAsync();
            try
            {
                var po = await _uow.PurchaseOrders.GetAsync(p => p.PoId == poId, q => q.Include(p => p.OrderLineItems));
                if (po == null) { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetNotFound(message: "Purchase order not found."); }
                if (po.Status != PurchaseOrderStatus.APPROVED) { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetConflict(message: "Only approved purchase orders can be received."); }
                if (request.Items.Select(i => i.LineItemId).Distinct().Count() != request.Items.Count)
                { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetBadRequest(message: "Receipt contains duplicate line items."); }

                foreach (var receipt in request.Items)
                {
                    var line = po.OrderLineItems.SingleOrDefault(l => l.LineItemId == receipt.LineItemId);
                    if (line == null || !InventoryQuantityRules.CanReceive(line.Quantity, line.ReceivedQuantity, receipt.Quantity))
                    { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetBadRequest(message: "Receipt quantity exceeds an order line's remaining quantity."); }
                    var inventory = await _uow.Inventories.GetAsync(i => i.WarehouseId == po.WarehouseId && i.VariantId == line.VariantId);
                    if (inventory == null)
                    {
                        inventory = new InventoryRecord { WarehouseId = po.WarehouseId, VariantId = line.VariantId, UpdatedAt = DateTime.UtcNow };
                        await _uow.Inventories.AddAsync(inventory);
                        await _uow.SaveChangeAsync();
                    }
                    var before = inventory.QuantityOnHand;
                    inventory.QuantityOnHand += receipt.Quantity;
                    inventory.OnOrderQuantity = Math.Max(0, inventory.OnOrderQuantity - receipt.Quantity);
                    inventory.UpdatedAt = DateTime.UtcNow;
                    line.ReceivedQuantity += receipt.Quantity;
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
                        PerformedByUserId = user.Id,
                        TransactionDate = DateTime.UtcNow
                    });
                }
                if (po.OrderLineItems.All(l => l.ReceivedQuantity == l.Quantity)) po.Status = PurchaseOrderStatus.DELIVERED;
                await _uow.SaveChangeAsync();
                await _uow.CommitTransactionAsync();
                return new ApiResponse().SetOk(_mapper.Map<PurchaseOrderResponse>(await GetDetailsAsync(poId)));
            }
            catch (DbUpdateConcurrencyException)
            {
                await _uow.RollbackTransactionAsync();
                return new ApiResponse().SetConflict(message: "Inventory changed while receiving the purchase order. Reload and retry.");
            }
            catch (Exception ex)
            {
                await _uow.RollbackTransactionAsync();
                return new ApiResponse().SetBadRequest(message: "Unable to receive purchase order: " + ex.Message);
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

        private static bool IsWarehouseManager(ClaimDTO claim) => string.Equals(claim.Role, Role.WAREHOUSE_MANAGER.ToString(), StringComparison.OrdinalIgnoreCase);
        private static ApiResponse Forbidden(string message) => new ApiResponse().SetApiResponse(System.Net.HttpStatusCode.Forbidden, false, message);
    }
}
