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
                if (project == null || supplier == null || supplier.IsDeleted || warehouse == null)
                    return await Abort(new ApiResponse().SetBadRequest(message: "Project, supplier, or warehouse does not exist."));
                if (project.Status is ProjectStatus.COMPLETED or ProjectStatus.CANCELLED or ProjectStatus.PAUSED)
                    return await Abort(new ApiResponse().SetConflict(message: "Paused or closed projects cannot accept new purchase orders."));
                if (warehouse.ManagerId != user.Id)
                    return await Abort(Forbidden("You may only create purchase orders for a warehouse you manage."));

                var resolved = new List<(OrderLineItemDto Item, MaterialVariant Variant, SupplierCatalog Catalog)>();
                foreach (var item in request.Items)
                {
                    if (item.Quantity <= 0 || item.UnitPrice < 0)
                        return await Abort(new ApiResponse().SetBadRequest(message: "Order quantities must be positive and prices cannot be negative."));
                    MaterialVariant? variant;
                    if (item.VariantId != 0)
                    {
                        variant = await _uow.MaterialVariants.GetByIdAsync(item.VariantId);
                    }
                    else
                    {
                        var candidates = await _uow.MaterialVariants.GetAllAsync(v =>
                            v.MaterialId == item.MaterialId && v.IsActive);
                        variant = candidates.Count == 1 ? candidates[0] : null;
                    }
                    if (variant == null || !variant.IsActive)
                        return await Abort(new ApiResponse().SetBadRequest(message:
                            "Material variant not found or inactive. MaterialId is valid only when it resolves to exactly one active variant."));
                    var variantDescription = DescribeVariant(variant);
                    var catalog = await _uow.SupplierCatalogs.GetAsync(c =>
                        c.SupplierId == request.SupplierId && c.VariantId == variant.VariantId && c.IsAvailable);
                    if (catalog == null)
                        return await Abort(new ApiResponse().SetBadRequest(message: $"{variantDescription} is not available from the selected supplier. Select one of its active catalog offers."));
                    if (catalog.UnitPrice <= 0)
                        return await Abort(new ApiResponse().SetConflict(message: $"{variantDescription} has an invalid supplier price. Update the catalog offer before ordering."));
                    if (request.ExpectedDeliveryDate.HasValue &&
                        request.ExpectedDeliveryDate.Value.Date < DateTime.UtcNow.Date.AddDays(catalog.LeadTimeDays))
                        return await Abort(new ApiResponse().SetBadRequest(message: $"Expected delivery for {variantDescription} must allow the supplier lead time of {catalog.LeadTimeDays} days."));
                    if (item.UnitPrice > 0 && item.UnitPrice != catalog.UnitPrice)
                        return await Abort(new ApiResponse().SetConflict(message: $"Submitted price for {variantDescription} differs from the current catalog price {catalog.UnitPrice}. Refresh the supplier offer before ordering."));
                    if (item.RequestItemId.HasValue)
                    {
                        var requestItem = await _uow.MaterialRequisitions.GetAsync(r => r.ItemId == item.RequestItemId.Value);
                        if (requestItem == null || requestItem.VariantId != variant.VariantId)
                            return await Abort(new ApiResponse().SetBadRequest(message: "RequestItemId does not match the ordered variant."));
                        var materialRequest = await _uow.MaterialRequests.GetAsync(r => r.RequestId == requestItem.RequestId);
                        if (materialRequest == null || materialRequest.ProjectId != request.ProjectId ||
                            materialRequest.Status is not (MaterialRequestStatuses.Approved or MaterialRequestStatuses.PartiallyApproved or MaterialRequestStatuses.Issued or MaterialRequestStatuses.PartiallyIssued))
                            return await Abort(new ApiResponse().SetBadRequest(message: "RequestItemId must belong to an approved material request for this project."));
                        if (materialRequest.WarehouseId != request.WarehouseId)
                            return await Abort(new ApiResponse().SetConflict(message: "A shortage-linked purchase order must deliver to the warehouse assigned to its material request."));
                        var linkedTask = materialRequest.TaskItem ?? (materialRequest.TaskId.HasValue
                            ? await _uow.TaskItems.GetByIdAsync(materialRequest.TaskId.Value)
                            : null);
                        if (linkedTask?.Status is cpms_Domain.Models.TaskStatus.COMPLETED or
                            cpms_Domain.Models.TaskStatus.CANCELLED or cpms_Domain.Models.TaskStatus.REJECTED)
                            return await Abort(new ApiResponse().SetConflict(message: "A shortage-linked purchase order cannot be created for a closed task."));

                        // Cover the task/variant shortage once, even when it was split across request rows.
                        // Legacy requests without a task remain isolated to their selected request item.
                        var hasTask = materialRequest.TaskId.HasValue;
                        var taskId = materialRequest.TaskId;
                        var relatedRequestItems = hasTask
                            ? await _uow.MaterialRequisitions.GetAllAsync(r =>
                                r.VariantId == variant.VariantId &&
                                r.MaterialRequest.ProjectId == materialRequest.ProjectId &&
                                r.MaterialRequest.TaskId == taskId &&
                                (r.MaterialRequest.Status == MaterialRequestStatuses.Approved ||
                                 r.MaterialRequest.Status == MaterialRequestStatuses.PartiallyApproved ||
                                 r.MaterialRequest.Status == MaterialRequestStatuses.Issued ||
                                 r.MaterialRequest.Status == MaterialRequestStatuses.PartiallyIssued))
                            : new List<MaterialRequisition> { requestItem };

                        // Only viable quantity still outstanding on a PO counts as procurement coverage.
                        // Accepted receipts are represented by ApprovedQuantity/reservations instead.
                        var existingLines = await _uow.OrderLineItems.GetAllAsync(l =>
                            l.RequestItemId.HasValue &&
                            l.VariantId == variant.VariantId &&
                            ((!hasTask && l.RequestItemId == requestItem.ItemId) ||
                             (hasTask &&
                              l.RequestItem!.MaterialRequest.ProjectId == materialRequest.ProjectId &&
                              l.RequestItem.MaterialRequest.TaskId == taskId)) &&
                            l.PurchaseOrder.Status != PurchaseOrderStatus.REJECTED &&
                            l.PurchaseOrder.Status != PurchaseOrderStatus.CANCELLED);

                        var grossShortage = relatedRequestItems.Sum(r =>
                            Math.Max(0, r.Quantity - r.ApprovedQuantity));
                        var coveredQuantity = existingLines.Sum(l =>
                            Math.Max(0, l.Quantity - l.ReceivedQuantity - l.DamagedQuantity - l.MissingQuantity));
                        var remainingShortage = Math.Max(0, grossShortage - coveredQuantity);
                        if (remainingShortage <= 0)
                            return await Abort(new ApiResponse().SetConflict(message: "This project task and material variant no longer has an unprocured shortage."));

                        var maximumOrderQuantity = Math.Max(remainingShortage, catalog.MinimumOrderQuantity);
                        if (item.Quantity > maximumOrderQuantity)
                            return await Abort(new ApiResponse().SetConflict(message:
                                $"Ordered quantity exceeds the remaining shortage. At most {maximumOrderQuantity} may be ordered, including any supplier minimum-order excess."));
                    }
                    if (item.Quantity < catalog.MinimumOrderQuantity)
                        return await Abort(new ApiResponse().SetBadRequest(message:
                            $"{variantDescription} requires a minimum order quantity of {catalog.MinimumOrderQuantity} {variant.Unit}."));

                    resolved.Add((item, variant, catalog));
                }
                if (resolved.GroupBy(x => x.Variant.VariantId).Any(g => g.Count() > 1))
                    return await Abort(new ApiResponse().SetBadRequest(message: "A material variant may only appear once per purchase order."));

                var total = resolved.Sum(x => x.Item.Quantity * x.Catalog.UnitPrice);
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
                    Project = project,
                    SupplierId = request.SupplierId,
                    Supplier = supplier,
                    WarehouseId = request.WarehouseId,
                    Warehouse = warehouse,
                    UserAccountId = user.Id,
                    TotalAmount = total,
                    OrderDate = DateTime.UtcNow,
                    ExpectedDeliveryDate = request.ExpectedDeliveryDate?.Date ??
                        DateTime.UtcNow.Date.AddDays(resolved.Max(x => x.Catalog.LeadTimeDays)),
                    Note = request.Note,
                    Status = PurchaseOrderStatus.PENDING
                };
                foreach (var entry in resolved)
                    po.OrderLineItems.Add(new OrderLineItem
                    {
                        VariantId = entry.Variant.VariantId,
                        Variant = entry.Variant,
                        RequestItemId = entry.Item.RequestItemId,
                        Quantity = entry.Item.Quantity,
                        UnitPrice = entry.Catalog.UnitPrice
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

        public async Task<ApiResponse> GetProcurementShortagesAsync()
        {
            var user = _claimService.GetUserClaim();
            if (!IsWarehouseManager(user))
                return Forbidden("Only warehouse managers may view procurement shortages.");

            var managedWarehouses = await _uow.Warehouses.GetAllAsync(w => w.ManagerId == user.Id);
            var managedWarehouseIds = managedWarehouses.Select(w => w.WarehouseId).ToList();
            if (managedWarehouseIds.Count == 0)
                return new ApiResponse().SetOk(new List<ProcurementShortageResponse>());

            var eligibleItems = await _uow.MaterialRequisitions.GetAllAsync(r =>
                    r.MaterialRequest.WarehouseId.HasValue &&
                    managedWarehouseIds.Contains(r.MaterialRequest.WarehouseId.Value) &&
                    r.MaterialRequest.Project.Status != ProjectStatus.COMPLETED &&
                     r.MaterialRequest.Project.Status != ProjectStatus.CANCELLED &&
                     (!r.MaterialRequest.TaskId.HasValue || r.MaterialRequest.TaskItem == null ||
                      (r.MaterialRequest.TaskItem.Status != cpms_Domain.Models.TaskStatus.COMPLETED &&
                       r.MaterialRequest.TaskItem.Status != cpms_Domain.Models.TaskStatus.CANCELLED &&
                       r.MaterialRequest.TaskItem.Status != cpms_Domain.Models.TaskStatus.REJECTED)) &&
                     (r.MaterialRequest.Status == MaterialRequestStatuses.Approved ||
                     r.MaterialRequest.Status == MaterialRequestStatuses.PartiallyApproved ||
                     r.MaterialRequest.Status == MaterialRequestStatuses.Issued ||
                     r.MaterialRequest.Status == MaterialRequestStatuses.PartiallyIssued),
                q => q.Include(r => r.MaterialRequest).ThenInclude(r => r.Project)
                      .Include(r => r.MaterialRequest).ThenInclude(r => r.Warehouse!)
                      .Include(r => r.Variant).ThenInclude(v => v.Material));

            if (eligibleItems.Count == 0)
                return new ApiResponse().SetOk(new List<ProcurementShortageResponse>());

            var eligibleItemIds = eligibleItems.Select(r => r.ItemId).ToList();
            var relevantVariantIds = eligibleItems.Select(r => r.VariantId).Distinct().ToList();
            var coveredLines = await _uow.OrderLineItems.GetAllAsync(l =>
                    l.RequestItemId.HasValue && eligibleItemIds.Contains(l.RequestItemId.Value) &&
                    l.PurchaseOrder.Status != PurchaseOrderStatus.REJECTED &&
                    l.PurchaseOrder.Status != PurchaseOrderStatus.CANCELLED,
                q => q.Include(l => l.PurchaseOrder).Include(l => l.RequestItem!));
            var catalogs = await _uow.SupplierCatalogs.GetAllAsync(c =>
                    relevantVariantIds.Contains(c.VariantId) && c.IsAvailable &&
                    c.Variant.IsActive && c.Variant.Material.IsActive,
                q => q.Include(c => c.Supplier)
                      .Include(c => c.Variant).ThenInclude(v => v.Material));

            var today = DateTime.UtcNow.Date;
            var shortages = new List<ProcurementShortageResponse>();
            var groups = eligibleItems.GroupBy(r => new
            {
                r.MaterialRequest.ProjectId,
                r.MaterialRequest.TaskId,
                LegacyRequestId = r.MaterialRequest.TaskId.HasValue ? 0 : r.RequestId,
                WarehouseId = r.MaterialRequest.WarehouseId!.Value,
                r.VariantId
            });

            foreach (var group in groups)
            {
                var groupItems = group.ToList();
                var groupItemIds = groupItems.Select(r => r.ItemId).ToHashSet();
                var grossShortage = groupItems.Sum(r => Math.Max(0, r.Quantity - r.ApprovedQuantity));
                var procurementCoverage = coveredLines
                    .Where(l => l.RequestItemId.HasValue && groupItemIds.Contains(l.RequestItemId.Value))
                    .Sum(l => Math.Max(0, l.Quantity - l.ReceivedQuantity - l.DamagedQuantity - l.MissingQuantity));
                var remainingShortage = Math.Max(0, grossShortage - procurementCoverage);
                if (remainingShortage <= 0) continue;

                var anchor = groupItems
                    .OrderByDescending(r => Math.Max(0, r.Quantity - r.ApprovedQuantity))
                    .ThenBy(r => r.ItemId)
                    .First();
                var warehouse = managedWarehouses.Single(w => w.WarehouseId == group.Key.WarehouseId);
                var offers = catalogs
                    .Where(c => c.VariantId == group.Key.VariantId)
                    .Select(c =>
                    {
                        var suggestedQuantity = Math.Max(remainingShortage, c.MinimumOrderQuantity);
                        return new ProcurementOfferResponse
                        {
                            CatalogId = c.CatalogId,
                            SupplierId = c.SupplierId,
                            SupplierName = c.Supplier.CompanyName,
                            SupplierSku = c.SupplierSku,
                            UnitPrice = c.UnitPrice,
                            MinimumOrderQuantity = c.MinimumOrderQuantity,
                            LeadTimeDays = c.LeadTimeDays,
                            EarliestDeliveryDate = today.AddDays(c.LeadTimeDays),
                            SuggestedOrderQuantity = suggestedQuantity,
                            ExpectedExcessStockQuantity = Math.Max(0, suggestedQuantity - remainingShortage),
                            SuggestedOrderTotal = suggestedQuantity * c.UnitPrice
                        };
                    })
                    .OrderBy(o => o.SuggestedOrderTotal)
                    .ThenBy(o => o.LeadTimeDays)
                    .ToList();

                shortages.Add(new ProcurementShortageResponse
                {
                    ProjectId = group.Key.ProjectId,
                    ProjectName = anchor.MaterialRequest.Project.ProjectName,
                    TaskId = group.Key.TaskId,
                    WarehouseId = group.Key.WarehouseId,
                    WarehouseName = warehouse.WarehouseName,
                    RequestItemId = anchor.ItemId,
                    RequestIds = groupItems.Select(r => r.RequestId).Distinct().OrderBy(id => id).ToList(),
                    VariantId = anchor.VariantId,
                    MaterialId = anchor.Variant.MaterialId,
                    MaterialName = anchor.Variant.Material.MaterialName,
                    VariantName = anchor.Variant.VariantName,
                    Sku = anchor.Variant.SKU,
                    Unit = anchor.Variant.Unit,
                    NeededByDate = groupItems.Min(r => r.NeededByDate),
                    GrossShortageQuantity = grossShortage,
                    ProcurementCoverageQuantity = procurementCoverage,
                    RemainingShortageQuantity = remainingShortage,
                    SupplierOffers = offers
                });
            }

            return new ApiResponse().SetOk(shortages
                .OrderBy(s => s.NeededByDate)
                .ThenBy(s => s.ProjectName)
                .ThenBy(s => s.MaterialName)
                .ToList());
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

        public async Task<ApiResponse> GetPurchaseOrderByIdAsync(int poId)
        {
            var po = await GetDetailsAsync(poId);
            if (po == null) return new ApiResponse().SetNotFound("Purchase order not found.");
            var user = _claimService.GetUserClaim();
            if (!CanReadPurchaseOrder(user, po))
                return Forbidden("You do not have access to this purchase order.");
            return new ApiResponse().SetOk(_mapper.Map<PurchaseOrderResponse>(po));
        }

        public async Task<ApiResponse> ApprovePurchaseOrderAsync(int poId, PurchaseOrderActionRequest? request = null)
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
                if (po.Project.Status is ProjectStatus.COMPLETED or ProjectStatus.CANCELLED or ProjectStatus.PAUSED)
                { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetConflict(message: "Purchase orders cannot be approved while the project is paused or closed."); }
                if (po.Status != PurchaseOrderStatus.PENDING) { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetConflict(message: "Only pending purchase orders can be approved."); }
                if (!OptionalRowVersionMatches(po.RowVersion, request?.RowVersion))
                { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetConflict(message: "Purchase order changed. Reload and retry."); }

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
                po.Note = AppendWorkflowNote(po.Note, "APPROVED", request?.Note);
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

        public async Task<ApiResponse> RejectPurchaseOrderAsync(int poId, PurchaseOrderActionRequest? request = null)
        {
            var user = _claimService.GetUserClaim();
            if (!IsPurchaseOrderApproverRole(user)) return Forbidden("Only administrators or project managers may reject purchase orders.");
            var po = await _uow.PurchaseOrders.GetAsync(p => p.PoId == poId, q => q.Include(p => p.Project));
            if (po == null) return new ApiResponse().SetNotFound(message: "Purchase order not found.");
            if (!CanApprovePurchaseOrder(user, po)) return Forbidden("You may only reject purchase orders for a project you manage.");
            if (po.UserAccountId == user.Id) return new ApiResponse().SetConflict(message: "The purchase-order creator cannot reject the same order.");
            if (po.Status != PurchaseOrderStatus.PENDING) return new ApiResponse().SetConflict(message: "Only pending purchase orders can be rejected.");
            if (!OptionalRowVersionMatches(po.RowVersion, request?.RowVersion))
                return new ApiResponse().SetConflict(message: "Purchase order changed. Reload and retry.");
            po.Status = PurchaseOrderStatus.REJECTED;
            po.Note = AppendWorkflowNote(po.Note, "REJECTED", request?.Note);
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
            await _uow.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                var po = await _uow.PurchaseOrders.GetAsync(p => p.PoId == poId,
                    q => q.Include(p => p.Project).Include(p => p.Warehouse).Include(p => p.OrderLineItems));
                if (po == null) { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetNotFound(message: "Purchase order not found."); }
                if (po.Warehouse.ManagerId != user.Id) { await _uow.RollbackTransactionAsync(); return Forbidden("You may only receive purchase orders into a warehouse you manage."); }
                if (po.Project.Status is ProjectStatus.COMPLETED or ProjectStatus.CANCELLED)
                { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetConflict(message: "Purchase orders cannot be received for a closed project."); }
                if (po.Project.Status == ProjectStatus.PAUSED &&
                    po.Status is not (PurchaseOrderStatus.SHIPPED or PurchaseOrderStatus.PARTIALLY_RECEIVED))
                { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetConflict(message: "While a project is paused, only already-shipped or partially received orders may be received."); }
                if (po.Status is not (PurchaseOrderStatus.APPROVED or PurchaseOrderStatus.PROCESSING or PurchaseOrderStatus.SHIPPED or PurchaseOrderStatus.PARTIALLY_RECEIVED))
                { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetConflict(message: "This purchase order cannot receive deliveries in its current state."); }
                if (!OptionalRowVersionMatches(po.RowVersion, request.RowVersion))
                { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetConflict(message: "Purchase order changed. Reload and retry."); }
                if (request.Items.Select(i => i.LineItemId).Distinct().Count() != request.Items.Count)
                { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetBadRequest(message: "Receipt contains duplicate line items."); }

                var linesById = po.OrderLineItems.ToDictionary(l => l.LineItemId);
                if (request.Items.Any(receipt => !linesById.ContainsKey(receipt.LineItemId)))
                { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetBadRequest(message: "Receipt contains a line that does not belong to this purchase order."); }
                foreach (var receipt in request.Items)
                {
                    var line = linesById[receipt.LineItemId];
                    var remaining = line.Quantity - line.ReceivedQuantity - line.DamagedQuantity - line.MissingQuantity;
                    var accountedNow = receipt.Quantity + receipt.DamagedQuantity + receipt.MissingQuantity;
                    if (accountedNow > remaining)
                    { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetBadRequest(message: "Receipt quantity exceeds an order line's remaining quantity."); }
                }
                if (request.IsFinalDelivery && po.OrderLineItems.Any(line =>
                {
                    var remaining = line.Quantity - line.ReceivedQuantity - line.DamagedQuantity - line.MissingQuantity;
                    var finalReceipt = request.Items.SingleOrDefault(item => item.LineItemId == line.LineItemId);
                    var accountedNow = finalReceipt == null
                        ? 0
                        : finalReceipt.Quantity + finalReceipt.DamagedQuantity + finalReceipt.MissingQuantity;
                    return accountedNow != remaining;
                }))
                { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetBadRequest(message: "A final delivery must account for every remaining unit as accepted, damaged, or missing."); }

                foreach (var receipt in request.Items)
                {
                    var line = linesById[receipt.LineItemId];
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
                    if (receipt.Quantity > 0 && line.RequestItemId.HasValue)
                        await AllocateAcceptedReceiptAsync(po, line, inventory, receipt.Quantity, user.Id);
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
                Items = po.OrderLineItems
                    .Where(l => l.Quantity > l.ReceivedQuantity + l.DamagedQuantity + l.MissingQuantity)
                    .Select(l => new ReceivePurchaseOrderItemRequest
                    {
                        LineItemId = l.LineItemId,
                        Quantity = l.Quantity - l.ReceivedQuantity - l.DamagedQuantity - l.MissingQuantity
                    }).ToList()
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

        private async Task AllocateAcceptedReceiptAsync(
            PurchaseOrder po,
            OrderLineItem line,
            InventoryRecord inventory,
            decimal acceptedQuantity,
            int performedByUserId)
        {
            if (!line.RequestItemId.HasValue || acceptedQuantity <= 0) return;

            var requestItem = await _uow.MaterialRequisitions.GetAsync(r => r.ItemId == line.RequestItemId.Value);
            if (requestItem == null || requestItem.VariantId != line.VariantId) return;
            var materialRequest = await _uow.MaterialRequests.GetAsync(r => r.RequestId == requestItem.RequestId);
            if (materialRequest == null || materialRequest.ProjectId != po.ProjectId ||
                materialRequest.WarehouseId != po.WarehouseId ||
                materialRequest.Status is not (MaterialRequestStatuses.Approved or
                    MaterialRequestStatuses.PartiallyApproved or
                    MaterialRequestStatuses.Issued or
                    MaterialRequestStatuses.PartiallyIssued))
                return;

            var project = await _uow.Projects.GetAsync(p => p.ProjectId == materialRequest.ProjectId);
            if (project == null || project.Status is ProjectStatus.COMPLETED or ProjectStatus.CANCELLED)
                return;
            var task = materialRequest.TaskItem ?? (materialRequest.TaskId.HasValue
                ? await _uow.TaskItems.GetByIdAsync(materialRequest.TaskId.Value)
                : null);
            if (task?.Status is cpms_Domain.Models.TaskStatus.COMPLETED or
                cpms_Domain.Models.TaskStatus.CANCELLED or cpms_Domain.Models.TaskStatus.REJECTED)
                return;

            var remainingRequestQuantity = Math.Max(0, requestItem.Quantity - requestItem.ApprovedQuantity);
            var allocationQuantity = Math.Min(acceptedQuantity, remainingRequestQuantity);
            if (allocationQuantity <= 0) return;

            var activeReservation = await _uow.InventoryReservations.GetAsync(r =>
                r.RequestItemId == requestItem.ItemId &&
                r.InventoryId == inventory.InventoryId &&
                r.Status == InventoryReservationStatuses.Active);
            if (activeReservation == null)
            {
                var reservation = new InventoryReservation
                {
                    InventoryId = inventory.InventoryId,
                    InventoryRecord = inventory,
                    RequestId = materialRequest.RequestId,
                    MaterialRequest = materialRequest,
                    RequestItemId = requestItem.ItemId,
                    RequestItem = requestItem,
                    Quantity = allocationQuantity,
                    Status = InventoryReservationStatuses.Active,
                    ReservedAt = DateTime.UtcNow,
                    CreatedBy = performedByUserId
                };
                await _uow.InventoryReservations.AddAsync(reservation);
                inventory.Reservations.Add(reservation);
                materialRequest.Reservations.Add(reservation);
                requestItem.Reservations.Add(reservation);
            }
            else
            {
                activeReservation.Quantity += allocationQuantity;
            }

            inventory.ReservedQuantity += allocationQuantity;
            requestItem.ApprovedQuantity += allocationQuantity;
            var requestItems = await _uow.MaterialRequisitions.GetAllAsync(r => r.RequestId == materialRequest.RequestId);
            materialRequest.Status = requestItems.Any(item => item.ApprovedQuantity < item.Quantity)
                ? MaterialRequestStatuses.PartiallyApproved
                : MaterialRequestStatuses.Approved;
            materialRequest.ApprovedByUserId = performedByUserId;
            materialRequest.ApprovedAt = DateTime.UtcNow;
        }

        public async Task<ApiResponse> MarkShippedAsync(int poId, PurchaseOrderActionRequest? request = null)
        {
            var user = _claimService.GetUserClaim();
            if (!IsWarehouseManager(user)) return Forbidden("Only warehouse managers may record shipment state.");
            var po = await _uow.PurchaseOrders.GetAsync(p => p.PoId == poId,
                q => q.Include(p => p.Project).Include(p => p.Warehouse));
            if (po == null) return new ApiResponse().SetNotFound("Purchase order not found.");
            if (po.Warehouse.ManagerId != user.Id) return Forbidden("You do not manage this purchase order's warehouse.");
            if (po.Project.Status is ProjectStatus.COMPLETED or ProjectStatus.CANCELLED or ProjectStatus.PAUSED)
                return new ApiResponse().SetConflict("Purchase orders cannot advance while the project is paused or closed.");
            if (po.Status is not (PurchaseOrderStatus.APPROVED or PurchaseOrderStatus.PROCESSING))
                return new ApiResponse().SetConflict("Only approved or processing purchase orders can be marked shipped.");
            if (!OptionalRowVersionMatches(po.RowVersion, request?.RowVersion))
                return new ApiResponse().SetConflict("Purchase order changed. Reload and retry.");
            po.Status = PurchaseOrderStatus.SHIPPED;
            po.Note = AppendWorkflowNote(po.Note, "SHIPPED", request?.Note);
            await _uow.SaveChangeAsync();
            return new ApiResponse().SetOk(_mapper.Map<PurchaseOrderResponse>(await GetDetailsAsync(poId)));
        }

        public async Task<ApiResponse> MarkProcessingAsync(int poId, PurchaseOrderActionRequest? request = null)
        {
            var user = _claimService.GetUserClaim();
            if (!IsWarehouseManager(user)) return Forbidden("Only warehouse managers may record supplier processing state.");
            var po = await _uow.PurchaseOrders.GetAsync(p => p.PoId == poId,
                q => q.Include(p => p.Project).Include(p => p.Warehouse));
            if (po == null) return new ApiResponse().SetNotFound("Purchase order not found.");
            if (po.Warehouse.ManagerId != user.Id) return Forbidden("You do not manage this purchase order's warehouse.");
            if (po.Project.Status is ProjectStatus.COMPLETED or ProjectStatus.CANCELLED or ProjectStatus.PAUSED)
                return new ApiResponse().SetConflict("Purchase orders cannot advance while the project is paused or closed.");
            if (po.Status != PurchaseOrderStatus.APPROVED)
                return new ApiResponse().SetConflict("Only an approved purchase order can be marked as supplier processing.");
            if (!OptionalRowVersionMatches(po.RowVersion, request?.RowVersion))
                return new ApiResponse().SetConflict("Purchase order changed. Reload and retry.");
            po.Status = PurchaseOrderStatus.PROCESSING;
            po.Note = AppendWorkflowNote(po.Note, "PROCESSING", request?.Note);
            await _uow.SaveChangeAsync();
            return new ApiResponse().SetOk(_mapper.Map<PurchaseOrderResponse>(await GetDetailsAsync(poId)));
        }

        public async Task<ApiResponse> CancelPurchaseOrderAsync(int poId, PurchaseOrderActionRequest? request = null)
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
                if (po.Status == PurchaseOrderStatus.SHIPPED)
                { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetConflict("A shipped purchase order cannot be cancelled. Receive it and record any missing or damaged quantities through final delivery processing."); }
                if (po.OrderLineItems.Any(line => line.ReceivedQuantity > 0 || line.DamagedQuantity > 0 || line.MissingQuantity > 0))
                { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetConflict("A partially received purchase order must be closed through delivery variance processing."); }
                if (!OptionalRowVersionMatches(po.RowVersion, request?.RowVersion))
                { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetConflict("Purchase order changed. Reload and retry."); }

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
                po.Note = AppendWorkflowNote(po.Note, "CANCELLED", request?.Note);
                await _uow.SaveChangeAsync();
                await _uow.CommitTransactionAsync();
                return new ApiResponse().SetOk(_mapper.Map<PurchaseOrderResponse>(await GetDetailsAsync(poId)));
            }
            catch
            {
                await _uow.RollbackTransactionAsync();
                throw;
            }
        }

        private static bool IsWarehouseManager(ClaimDTO claim) => string.Equals(claim.Role, Role.WAREHOUSE_MANAGER.ToString(), StringComparison.OrdinalIgnoreCase);
        private static bool OptionalRowVersionMatches(byte[] current, string? supplied)
        {
            if (string.IsNullOrWhiteSpace(supplied) || current.Length == 0) return true;
            try { return current.AsSpan().SequenceEqual(Convert.FromBase64String(supplied)); }
            catch (FormatException) { return false; }
        }
        private static string? AppendWorkflowNote(string? current, string action, string? note)
        {
            if (string.IsNullOrWhiteSpace(note)) return current;
            var entry = $"[{action} {DateTime.UtcNow:O}] {note.Trim()}";
            return string.IsNullOrWhiteSpace(current) ? entry : $"{current}{Environment.NewLine}{entry}";
        }
        private static string DescribeVariant(MaterialVariant variant) =>
            string.IsNullOrWhiteSpace(variant.SKU)
                ? $"{variant.VariantName} (variant #{variant.VariantId})"
                : $"{variant.VariantName} [SKU: {variant.SKU}]";
        private static bool IsPurchaseOrderApproverRole(ClaimDTO claim) =>
            string.Equals(claim.Role, Role.ADMIN.ToString(), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(claim.Role, Role.PM.ToString(), StringComparison.OrdinalIgnoreCase);
        private static bool CanApprovePurchaseOrder(ClaimDTO claim, PurchaseOrder order) =>
            string.Equals(claim.Role, Role.ADMIN.ToString(), StringComparison.OrdinalIgnoreCase) ||
            (string.Equals(claim.Role, Role.PM.ToString(), StringComparison.OrdinalIgnoreCase) && order.Project.PMUserID == claim.Id);
        private static bool CanReadPurchaseOrder(ClaimDTO claim, PurchaseOrder order) =>
            string.Equals(claim.Role, Role.ADMIN.ToString(), StringComparison.OrdinalIgnoreCase) ||
            (string.Equals(claim.Role, Role.PM.ToString(), StringComparison.OrdinalIgnoreCase) && order.Project.PMUserID == claim.Id) ||
            (IsWarehouseManager(claim) && order.Warehouse.ManagerId == claim.Id);
        private static ApiResponse Forbidden(string message) => new ApiResponse().SetApiResponse(System.Net.HttpStatusCode.Forbidden, false, message);
    }
}
