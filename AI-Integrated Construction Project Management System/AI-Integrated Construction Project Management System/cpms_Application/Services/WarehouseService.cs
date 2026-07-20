using AutoMapper;
using cpms_Application.Interfaces;
using cpms_Application.Request.Warehouse;
using cpms_Application.Response;
using cpms_Application.Response.Inventory;
using cpms_Application.Response.Warehouse;
using cpms_Domain.Models;
using cpms_Domain;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace cpms_Application.Services
{
    public class WarehouseService : IWarehouseService
    {
        private readonly IUnitOfWork _uow;
        private readonly IMapper _mapper;
        private readonly IClaimService _claimService;

        public WarehouseService(IUnitOfWork uow, IMapper mapper, IClaimService claimService)
        {
            _uow = uow;
            _mapper = mapper;
            _claimService = claimService;
        }

        public async Task<ApiResponse> CreateWarehouseAsync(CreateWarehouseRequest request)
        {
            var user = _claimService.GetUserClaim();
            if (!string.Equals(user.Role, Role.ADMIN.ToString(), StringComparison.OrdinalIgnoreCase)) return Forbidden("Only administrators may create warehouses.");
            var warehouse = _mapper.Map<Warehouse>(request);
            warehouse.ManagerId = request.ManagerId > 0 ? request.ManagerId : user.Id;
            var manager = await _uow.UserAccounts.GetByIdAsync(warehouse.ManagerId);
            if (manager == null || manager.Role != Role.WAREHOUSE_MANAGER || manager.IsEmailVerified != true)
                return new ApiResponse().SetBadRequest(message: "Warehouse manager must be a verified WAREHOUSE_MANAGER account.");
            var duplicate = await _uow.Warehouses.GetAsync(w => w.WarehouseName == request.WarehouseName.Trim());
            if (duplicate != null) return new ApiResponse().SetConflict("An active warehouse already uses this name.");
            warehouse.WarehouseName = request.WarehouseName.Trim();
            warehouse.Location = request.Location.Trim();
            warehouse.Manager = manager;
            await _uow.Warehouses.AddAsync(warehouse);
            await _uow.SaveChangeAsync();
            return new ApiResponse().SetApiResponse(System.Net.HttpStatusCode.Created, true,
                result: _mapper.Map<WarehouseResponse>(warehouse));
        }

        public async Task<ApiResponse> UpdateWarehouseAsync(int warehouseId, UpdateWarehouseRequest request)
        {
            var user = _claimService.GetUserClaim();
            if (!IsAdmin(user)) return Forbidden("Only administrators may update warehouses.");
            var warehouse = await _uow.Warehouses.GetByIdAsync(warehouseId);
            if (warehouse == null) return new ApiResponse().SetNotFound("Warehouse not found.");
            var manager = await _uow.UserAccounts.GetByIdAsync(request.ManagerId);
            if (manager == null || manager.Role != Role.WAREHOUSE_MANAGER || manager.IsEmailVerified != true)
                return new ApiResponse().SetBadRequest("Warehouse manager must be a verified WAREHOUSE_MANAGER account.");
            var normalizedName = request.WarehouseName.Trim();
            var duplicate = await _uow.Warehouses.GetAsync(w => w.WarehouseId != warehouseId && w.WarehouseName == normalizedName);
            if (duplicate != null) return new ApiResponse().SetConflict("Another active warehouse already uses this name.");
            warehouse.WarehouseName = normalizedName;
            warehouse.Location = request.Location.Trim();
            warehouse.ManagerId = request.ManagerId;
            warehouse.Manager = manager;
            warehouse.ModifiedBy = user.Id;
            warehouse.ModifiedDate = DateTime.UtcNow;
            await _uow.SaveChangeAsync();
            return new ApiResponse().SetOk(_mapper.Map<WarehouseResponse>(warehouse));
        }

        public async Task<ApiResponse> GetAllWarehousesAsync()
        {
            var user = _claimService.GetUserClaim();
            if (!IsAdmin(user) && !IsWarehouseManager(user)) return Forbidden("Warehouse access is not allowed for this role.");
            var list = await _uow.Warehouses.GetAllAsync(IsAdmin(user) ? null : w => w.ManagerId == user.Id,
                q => q.Include(w => w.Manager).Include(w => w.InventoryRecords));
            return new ApiResponse().SetOk(_mapper.Map<List<WarehouseResponse>>(list));
        }

        public async Task<ApiResponse> GetWarehouseByIdAsync(int warehouseId)
        {
            var access = await AuthorizeReadAsync(warehouseId);
            if (access != null) return access;
            var warehouse = await _uow.Warehouses.GetAsync(w => w.WarehouseId == warehouseId,
                q => q.Include(w => w.Manager).Include(w => w.InventoryRecords));
            return warehouse == null
                ? new ApiResponse().SetNotFound("Warehouse not found.")
                : new ApiResponse().SetOk(_mapper.Map<WarehouseResponse>(warehouse));
        }

        public async Task<ApiResponse> GetWarehouseInventoryAsync(int warehouseId)
        {
            var access = await AuthorizeReadAsync(warehouseId);
            if (access != null) return access;
            var records = await _uow.Inventories.GetAllAsync(i => i.WarehouseId == warehouseId,
                q => q.Include(i => i.Warehouse).Include(i => i.Variant).ThenInclude(v => v.Material));
            return new ApiResponse().SetOk(_mapper.Map<List<InventoryReportResponse>>(records));
        }

        public async Task<ApiResponse> GetInventoryAsync(int warehouseId, int variantId)
        {
            var access = await AuthorizeReadAsync(warehouseId);
            if (access != null) return access;
            var record = await _uow.Inventories.GetAsync(i => i.WarehouseId == warehouseId && i.VariantId == variantId,
                q => q.Include(i => i.Warehouse).Include(i => i.Variant).ThenInclude(v => v.Material));
            return record == null ? new ApiResponse().SetNotFound(message: "Inventory record not found.")
                : new ApiResponse().SetOk(_mapper.Map<InventoryReportResponse>(record));
        }

        public async Task<ApiResponse> AdjustInventoryAsync(InventoryAdjustmentRequest request)
        {
            var user = _claimService.GetUserClaim();
            if (!IsWarehouseManager(user)) return Forbidden("Only warehouse managers may request inventory adjustments.");
            var warehouse = await _uow.Warehouses.GetByIdAsync(request.WarehouseId);
            if (warehouse == null) return new ApiResponse().SetNotFound("Warehouse not found.");
            if (warehouse.ManagerId != user.Id) return Forbidden("You may only request adjustments for a warehouse you manage.");
            var variant = await _uow.MaterialVariants.GetByIdAsync(request.VariantId);
            if (variant == null || !variant.IsActive) return new ApiResponse().SetBadRequest("Material variant not found or inactive.");
            if (request.QuantityDelta == 0 || !InventoryAdjustmentReasons.All.Contains(request.ReasonCode))
                return new ApiResponse().SetBadRequest("A non-zero quantity and standardized reason code are required.");
            var pending = await _uow.InventoryAdjustments.GetAsync(x => x.WarehouseId == request.WarehouseId &&
                x.VariantId == request.VariantId && x.Status == InventoryAdjustmentStatuses.Pending);
            if (pending != null) return new ApiResponse().SetConflict("A pending adjustment already exists for this warehouse and variant.");
            var adjustment = new InventoryAdjustment
            {
                WarehouseId = request.WarehouseId,
                VariantId = request.VariantId,
                QuantityDelta = request.QuantityDelta,
                ReasonCode = request.ReasonCode,
                Note = request.Note,
                RequestedByUserId = user.Id,
                RequestedAt = DateTime.UtcNow
            };
            await _uow.InventoryAdjustments.AddAsync(adjustment);
            await _uow.SaveChangeAsync();
            return new ApiResponse().SetApiResponse(System.Net.HttpStatusCode.Created, true, result: new
            {
                adjustment.AdjustmentId,
                adjustment.Status,
                RowVersion = Convert.ToBase64String(adjustment.RowVersion)
            });
        }

        public async Task<ApiResponse> GetInventoryAdjustmentsAsync(string? status)
        {
            var user = _claimService.GetUserClaim();
            if (!IsAdmin(user) && !IsWarehouseManager(user)) return Forbidden("Inventory adjustment access is not allowed.");
            var isAdmin = IsAdmin(user);
            var normalizedStatus = string.IsNullOrWhiteSpace(status) ? null : status.Trim().ToUpperInvariant();
            var records = await _uow.InventoryAdjustments.GetAllAsync(x =>
                (normalizedStatus == null || x.Status == normalizedStatus) &&
                (isAdmin || x.Warehouse.ManagerId == user.Id),
                q => q.Include(x => x.Warehouse).Include(x => x.Variant));
            return new ApiResponse().SetOk(records.OrderByDescending(x => x.RequestedAt).Select(x => new
            {
                x.AdjustmentId, x.WarehouseId, x.VariantId, x.QuantityDelta, x.ReasonCode, x.Note,
                x.Status, x.RequestedByUserId, x.ReviewedByUserId, x.RequestedAt, x.ReviewedAt, x.ReviewNote,
                RowVersion = Convert.ToBase64String(x.RowVersion)
            }));
        }

        public async Task<ApiResponse> ReviewInventoryAdjustmentAsync(int adjustmentId, bool approve, ReviewInventoryAdjustmentRequest review)
        {
            var user = _claimService.GetUserClaim();
            if (!IsAdmin(user)) return Forbidden("Only administrators may review inventory adjustments.");
            var adjustment = await _uow.InventoryAdjustments.GetByIdAsync(adjustmentId);
            if (adjustment == null) return new ApiResponse().SetNotFound("Inventory adjustment not found.");
            if (adjustment.RequestedByUserId == user.Id) return new ApiResponse().SetConflict("The requester cannot approve their own adjustment.");
            if (adjustment.Status != InventoryAdjustmentStatuses.Pending) return new ApiResponse().SetConflict("Only pending adjustments can be reviewed.");
            if (string.IsNullOrWhiteSpace(review.RowVersion) || !Convert.ToBase64String(adjustment.RowVersion).Equals(review.RowVersion, StringComparison.Ordinal))
                return new ApiResponse().SetConflict("Inventory adjustment changed. Reload and retry.");
            if (!approve)
            {
                adjustment.Status = InventoryAdjustmentStatuses.Rejected;
                adjustment.ReviewedByUserId = user.Id;
                adjustment.ReviewedAt = DateTime.UtcNow;
                adjustment.ReviewNote = review.ReviewNote;
                await _uow.SaveChangeAsync();
                return new ApiResponse().SetOk("Inventory adjustment rejected.");
            }
            adjustment.ReviewNote = review.ReviewNote;
            return await ApplyInventoryAdjustmentAsync(adjustment, user.Id);
        }

        private async Task<ApiResponse> ApplyInventoryAdjustmentAsync(InventoryAdjustment request, int reviewerId)
        {
            if (request.QuantityDelta == 0) return new ApiResponse().SetBadRequest(message: "QuantityDelta cannot be zero.");
            var managedWarehouse = await _uow.Warehouses.GetByIdAsync(request.WarehouseId);
            if (managedWarehouse == null) return new ApiResponse().SetNotFound(message: "Warehouse not found.");
            await _uow.BeginTransactionAsync();
            try
            {
                var inventory = await _uow.Inventories.GetAsync(i => i.WarehouseId == request.WarehouseId && i.VariantId == request.VariantId);
                if (inventory == null)
                {
                    if (request.QuantityDelta < 0) { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetBadRequest(message: "Cannot create inventory with a negative adjustment."); }
                    var variant = await _uow.MaterialVariants.GetByIdAsync(request.VariantId);
                    if (await _uow.Warehouses.GetByIdAsync(request.WarehouseId) == null || variant == null || !variant.IsActive)
                    { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetBadRequest(message: "Warehouse or material variant not found."); }
                    inventory = new InventoryRecord
                    {
                        WarehouseId = request.WarehouseId,
                        VariantId = request.VariantId,
                        QuantityOnHand = 0,
                        UpdatedAt = DateTime.UtcNow
                    };
                    await _uow.Inventories.AddAsync(inventory);
                    await _uow.SaveChangeAsync();
                }

                var before = inventory.QuantityOnHand;
                var after = before + request.QuantityDelta;
                if (!InventoryQuantityRules.CanAdjust(before, inventory.ReservedQuantity, inventory.QuarantineQuantity, request.QuantityDelta))
                { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetConflict(message: "Adjustment cannot reduce stock below reserved and quarantined quantities."); }
                inventory.QuantityOnHand = after;
                inventory.UpdatedAt = DateTime.UtcNow;
                await _uow.InventoryTransactions.AddAsync(new InventoryTransaction
                {
                    InventoryId = inventory.InventoryId,
                    WarehouseId = inventory.WarehouseId,
                    VariantId = inventory.VariantId,
                    TransactionType = InventoryTransactionTypes.Adjustment,
                    Quantity = request.QuantityDelta,
                    QuantityBefore = before,
                    QuantityAfter = after,
                    Note = request.Note,
                    PerformedByUserId = reviewerId,
                    TransactionDate = DateTime.UtcNow
                });
                request.Status = InventoryAdjustmentStatuses.Approved;
                request.ReviewedByUserId = reviewerId;
                request.ReviewedAt = DateTime.UtcNow;
                await _uow.SaveChangeAsync();
                await _uow.CommitTransactionAsync();
                return await GetInventoryAsync(request.WarehouseId, request.VariantId);
            }
            catch (DbUpdateConcurrencyException)
            {
                await _uow.RollbackTransactionAsync();
                return new ApiResponse().SetConflict(message: "Inventory has changed. Reload and retry.");
            }
            catch
            {
                await _uow.RollbackTransactionAsync();
                throw;
            }
        }

        public async Task<ApiResponse> GetTransactionsAsync(int? warehouseId, int? variantId)
        {
            var user = _claimService.GetUserClaim();
            var isAdmin = IsAdmin(user);
            if (!isAdmin && !IsWarehouseManager(user)) return Forbidden("Warehouse transaction access is not allowed for this role.");
            if (warehouseId.HasValue && !isAdmin)
            {
                var access = await AuthorizeReadAsync(warehouseId.Value);
                if (access != null) return access;
            }
            var managedWarehouseIds = isAdmin
                ? new List<int>()
                : (await _uow.Warehouses.GetAllAsync(w => w.ManagerId == user.Id)).Select(w => w.WarehouseId).ToList();
            var transactions = await _uow.InventoryTransactions.GetAllIgnoringQueryFiltersAsync(t =>
                (!warehouseId.HasValue || t.WarehouseId == warehouseId.Value) &&
                (!variantId.HasValue || t.VariantId == variantId.Value) &&
                (isAdmin || managedWarehouseIds.Contains(t.WarehouseId)));
            return new ApiResponse().SetOk(_mapper.Map<List<InventoryTransactionResponse>>(transactions.OrderByDescending(t => t.TransactionDate)));
        }

        public async Task<ApiResponse> StartPhysicalCountAsync(StartPhysicalCountRequest request)
        {
            var user = _claimService.GetUserClaim();
            if (!IsWarehouseManager(user)) return Forbidden("Only warehouse managers may start physical counts.");
            var warehouse = await _uow.Warehouses.GetByIdAsync(request.WarehouseId);
            if (warehouse == null) return new ApiResponse().SetNotFound("Warehouse not found.");
            if (warehouse.ManagerId != user.Id) return Forbidden("You may only count a warehouse you manage.");
            await _uow.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                var open = await _uow.PhysicalCountSessions.GetAsync(x => x.WarehouseId == request.WarehouseId &&
                    (x.Status == PhysicalCountStatuses.Draft || x.Status == PhysicalCountStatuses.PendingApproval));
                if (open != null)
                {
                    await _uow.RollbackTransactionAsync();
                    return new ApiResponse().SetConflict("This warehouse already has an open physical count.");
                }
                var inventories = await _uow.Inventories.GetAllAsync(i => i.WarehouseId == request.WarehouseId &&
                    (request.VariantIds.Count == 0 || request.VariantIds.Contains(i.VariantId)));
                if (inventories.Count == 0)
                {
                    await _uow.RollbackTransactionAsync();
                    return new ApiResponse().SetBadRequest("No inventory records match this count scope.");
                }
                var session = new PhysicalCountSession
                {
                    WarehouseId = request.WarehouseId,
                    CreatedByUserId = user.Id,
                    StartedAt = DateTime.UtcNow,
                    Note = request.Note
                };
                await _uow.PhysicalCountSessions.AddAsync(session);
                await _uow.SaveChangeAsync();
                await _uow.PhysicalCountLines.AddRangeAsync(inventories.Select(i => new PhysicalCountLine
                {
                    SessionId = session.SessionId,
                    InventoryId = i.InventoryId,
                    VariantId = i.VariantId,
                    ExpectedQuantity = i.QuantityOnHand,
                    ExpectedInventoryRowVersion = i.RowVersion.ToArray()
                }).ToList());
                await _uow.SaveChangeAsync();
                await _uow.CommitTransactionAsync();
                return new ApiResponse().SetApiResponse(System.Net.HttpStatusCode.Created, true, result: new
                {
                    session.SessionId,
                    session.Status,
                    LineCount = inventories.Count,
                    RowVersion = Convert.ToBase64String(session.RowVersion)
                });
            }
            catch { await _uow.RollbackTransactionAsync(); throw; }
        }

        public async Task<ApiResponse> SubmitPhysicalCountAsync(int sessionId, SubmitPhysicalCountRequest request)
        {
            var user = _claimService.GetUserClaim();
            if (!IsWarehouseManager(user)) return Forbidden("Only warehouse managers may submit physical counts.");
            var session = await _uow.PhysicalCountSessions.GetAsync(x => x.SessionId == sessionId,
                q => q.Include(x => x.Warehouse).Include(x => x.Lines));
            if (session == null) return new ApiResponse().SetNotFound("Physical count session not found.");
            if (session.Warehouse.ManagerId != user.Id) return Forbidden("You do not manage this count's warehouse.");
            if (session.Status != PhysicalCountStatuses.Draft) return new ApiResponse().SetConflict("Only draft counts can be submitted.");
            if (!RowVersionMatches(session.RowVersion, request.RowVersion)) return new ApiResponse().SetConflict("Physical count changed. Reload and retry.");
            if (request.Lines.Count != session.Lines.Count || request.Lines.Select(x => x.LineId).Distinct().Count() != session.Lines.Count ||
                request.Lines.Any(x => x.ActualQuantity < 0 || session.Lines.All(l => l.LineId != x.LineId)))
                return new ApiResponse().SetBadRequest("Submit one nonnegative actual quantity for every count line.");
            foreach (var line in session.Lines)
                line.ActualQuantity = request.Lines.Single(x => x.LineId == line.LineId).ActualQuantity;
            session.Status = PhysicalCountStatuses.PendingApproval;
            session.SubmittedAt = DateTime.UtcNow;
            await _uow.SaveChangeAsync();
            return new ApiResponse().SetOk(new { session.SessionId, session.Status, RowVersion = Convert.ToBase64String(session.RowVersion) });
        }

        public async Task<ApiResponse> ReviewPhysicalCountAsync(int sessionId, bool approve, ReviewPhysicalCountRequest request)
        {
            var user = _claimService.GetUserClaim();
            if (!IsAdmin(user)) return Forbidden("Only administrators may review physical counts.");
            await _uow.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                var session = await _uow.PhysicalCountSessions.GetAsync(x => x.SessionId == sessionId,
                    q => q.Include(x => x.Lines).ThenInclude(l => l.InventoryRecord));
                if (session == null) { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetNotFound("Physical count session not found."); }
                if (session.CreatedByUserId == user.Id) { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetConflict("The count creator cannot approve their own count."); }
                if (session.Status != PhysicalCountStatuses.PendingApproval) { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetConflict("Only submitted counts can be reviewed."); }
                if (!RowVersionMatches(session.RowVersion, request.RowVersion)) { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetConflict("Physical count changed. Reload and retry."); }
                session.ReviewedByUserId = user.Id;
                session.ReviewedAt = DateTime.UtcNow;
                session.ReviewNote = request.ReviewNote;
                if (!approve)
                {
                    session.Status = PhysicalCountStatuses.Rejected;
                    await _uow.SaveChangeAsync();
                    await _uow.CommitTransactionAsync();
                    return new ApiResponse().SetOk("Physical count rejected; inventory was not changed.");
                }
                foreach (var line in session.Lines)
                {
                    var inventory = line.InventoryRecord;
                    if (!CryptographicOperations.FixedTimeEquals(
                            inventory.RowVersion,
                            line.ExpectedInventoryRowVersion))
                    {
                        await _uow.RollbackTransactionAsync();
                        return new ApiResponse().SetConflict($"Inventory for variant {line.VariantId} changed after the count started. Recount this item.");
                    }
                    var actual = line.ActualQuantity!.Value;
                    if (actual < inventory.ReservedQuantity + inventory.QuarantineQuantity)
                    { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetConflict($"Counted quantity for variant {line.VariantId} is below reserved and quarantined stock."); }
                    var before = inventory.QuantityOnHand;
                    var delta = actual - before;
                    inventory.QuantityOnHand = actual;
                    inventory.UpdatedAt = DateTime.UtcNow;
                    if (delta != 0)
                        await _uow.InventoryTransactions.AddAsync(new InventoryTransaction
                        {
                            InventoryId = inventory.InventoryId, WarehouseId = session.WarehouseId, VariantId = line.VariantId,
                            TransactionType = InventoryTransactionTypes.PhysicalCount, Quantity = delta,
                            QuantityBefore = before, QuantityAfter = actual, ReferenceId = session.SessionId,
                            ReferenceType = "PHYSICAL_COUNT", Note = request.ReviewNote,
                            PerformedByUserId = user.Id, TransactionDate = DateTime.UtcNow
                        });
                }
                session.Status = PhysicalCountStatuses.Approved;
                await _uow.SaveChangeAsync();
                await _uow.CommitTransactionAsync();
                return new ApiResponse().SetOk("Physical count approved and variances posted.");
            }
            catch (DbUpdateConcurrencyException) { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetConflict("Inventory changed during count approval. Reload and retry."); }
            catch { await _uow.RollbackTransactionAsync(); throw; }
        }

        public async Task<ApiResponse> GetPhysicalCountsAsync(int? warehouseId, string? status)
        {
            var user = _claimService.GetUserClaim();
            if (!IsAdmin(user) && !IsWarehouseManager(user)) return Forbidden("Physical count access is not allowed.");
            var normalized = string.IsNullOrWhiteSpace(status) ? null : status.Trim().ToUpperInvariant();
            var sessions = await _uow.PhysicalCountSessions.GetAllAsync(x =>
                (!warehouseId.HasValue || x.WarehouseId == warehouseId) &&
                (normalized == null || x.Status == normalized) &&
                (IsAdmin(user) || x.Warehouse.ManagerId == user.Id),
                q => q.Include(x => x.Warehouse).Include(x => x.Lines));
            return new ApiResponse().SetOk(sessions.OrderByDescending(x => x.StartedAt).Select(x => new
            {
                x.SessionId, x.WarehouseId, x.Status, x.StartedAt, x.SubmittedAt, x.ReviewedAt, x.Note, x.ReviewNote,
                RowVersion = Convert.ToBase64String(x.RowVersion),
                Lines = x.Lines.Select(l => new { l.LineId, l.VariantId, l.ExpectedQuantity, l.ActualQuantity, l.VarianceQuantity })
            }));
        }

        public async Task<ApiResponse> ReturnInventoryAsync(InventoryReturnRequest request)
        {
            var user = _claimService.GetUserClaim();
            if (!IsWarehouseManager(user)) return Forbidden("Only warehouse managers may record inventory returns.");
            if (request.MaterialRequestId <= 0)
                return new ApiResponse().SetBadRequest(message: "MaterialRequestId is required. Use inventory adjustment for unlinked stock corrections.");
            var warehouse = await _uow.Warehouses.GetByIdAsync(request.WarehouseId);
            if (warehouse == null) return new ApiResponse().SetNotFound(message: "Warehouse not found.");
            if (warehouse.ManagerId != user.Id) return Forbidden("You may only return inventory to a warehouse you manage.");
            var variant = await _uow.MaterialVariants.GetByIdAsync(request.VariantId);
            if (variant == null || !variant.IsActive)
                return new ApiResponse().SetBadRequest(message: "Material variant not found or inactive.");

            await _uow.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                var materialRequest = await _uow.MaterialRequests.GetAsync(
                    x => x.RequestId == request.MaterialRequestId,
                    query => query.Include(x => x.Requisitions));
                if (materialRequest == null)
                    return await Rollback(new ApiResponse().SetBadRequest(message: "Referenced material request was not found."));
                if (materialRequest.Status is not (MaterialRequestStatuses.Issued or MaterialRequestStatuses.PartiallyIssued))
                    return await Rollback(new ApiResponse().SetConflict(message: "Only issued or partially issued material requests can be returned."));
                if (materialRequest.WarehouseId != request.WarehouseId)
                    return await Rollback(new ApiResponse().SetBadRequest(message: "The material request was issued from a different warehouse."));

                var requestItem = materialRequest.Requisitions.SingleOrDefault(x => x.VariantId == request.VariantId);
                if (requestItem == null || requestItem.IssuedQuantity <= 0)
                    return await Rollback(new ApiResponse().SetBadRequest(message: "The selected variant was not issued by this material request."));

                var previousReturns = await _uow.MaterialReturns.GetAllAsync(x =>
                    x.MaterialRequestId == materialRequest.RequestId &&
                    x.WarehouseId == request.WarehouseId &&
                    x.VariantId == request.VariantId);
                var legacyReturns = await _uow.InventoryTransactions.GetAllIgnoringQueryFiltersAsync(x =>
                    x.TransactionType == InventoryTransactionTypes.Return &&
                    x.ReferenceType == "MATERIAL_REQUEST" && x.ReferenceId == materialRequest.RequestId &&
                    x.WarehouseId == request.WarehouseId && x.VariantId == request.VariantId);
                var remainingReturnable = requestItem.IssuedQuantity - previousReturns.Sum(x => x.Quantity) - legacyReturns.Sum(x => x.Quantity);
                if (request.Quantity > remainingReturnable)
                    return await Rollback(new ApiResponse().SetConflict(
                        message: $"Return quantity exceeds the remaining returnable quantity of {Math.Max(0, remainingReturnable)}."));

                var inventory = await _uow.Inventories.GetAsync(x => x.WarehouseId == request.WarehouseId && x.VariantId == request.VariantId);
                if (inventory == null)
                {
                    inventory = new InventoryRecord
                    {
                        WarehouseId = request.WarehouseId,
                        VariantId = request.VariantId,
                        UpdatedAt = DateTime.UtcNow,
                        CreatedBy = user.Id
                    };
                    await _uow.Inventories.AddAsync(inventory);
                    await _uow.SaveChangeAsync();
                }
                else if (!string.IsNullOrWhiteSpace(request.RowVersion) &&
                         !Convert.ToBase64String(inventory.RowVersion).Equals(request.RowVersion, StringComparison.Ordinal))
                    return await Rollback(new ApiResponse().SetConflict(message: "Inventory has changed. Reload and retry."));

                var before = inventory.QuantityOnHand;
                inventory.QuantityOnHand += request.Quantity;
                if (request.Condition == MaterialReturnConditions.Quarantined)
                    inventory.QuarantineQuantity += request.Quantity;
                inventory.UpdatedAt = DateTime.UtcNow;
                var materialReturn = new MaterialReturn
                {
                    MaterialRequestId = materialRequest.RequestId,
                    WarehouseId = request.WarehouseId,
                    VariantId = request.VariantId,
                    Quantity = request.Quantity,
                    ReasonCode = request.ReasonCode,
                    Condition = request.Condition,
                    Note = request.Note,
                    RecordedByUserId = user.Id,
                    ReturnedAt = DateTime.UtcNow
                };
                await _uow.MaterialReturns.AddAsync(materialReturn);
                await _uow.SaveChangeAsync();
                await _uow.InventoryTransactions.AddAsync(new InventoryTransaction
                {
                    InventoryId = inventory.InventoryId,
                    WarehouseId = inventory.WarehouseId,
                    VariantId = inventory.VariantId,
                    TransactionType = InventoryTransactionTypes.Return,
                    Quantity = request.Quantity,
                    QuantityBefore = before,
                    QuantityAfter = inventory.QuantityOnHand,
                    ReferenceId = materialReturn.ReturnId,
                    ReferenceType = "MATERIAL_RETURN",
                    Note = request.Note,
                    UnitCost = inventory.AverageUnitCost,
                    TotalValue = request.Quantity * inventory.AverageUnitCost,
                    PerformedByUserId = user.Id,
                    TransactionDate = DateTime.UtcNow
                });
                await _uow.SaveChangeAsync();
                await _uow.CommitTransactionAsync();
                return await GetInventoryAsync(request.WarehouseId, request.VariantId);
            }
            catch (DbUpdateConcurrencyException)
            {
                await _uow.RollbackTransactionAsync();
                return new ApiResponse().SetConflict(message: "Inventory changed while recording the return. Reload and retry.");
            }
            catch (Exception)
            {
                await _uow.RollbackTransactionAsync();
                return new ApiResponse().SetApiResponse(System.Net.HttpStatusCode.InternalServerError, false, "Unable to record inventory return.");
            }
        }

        private async Task<ApiResponse> Rollback(ApiResponse response)
        {
            await _uow.RollbackTransactionAsync();
            return response;
        }

        private static ApiResponse Forbidden(string message) => new ApiResponse().SetApiResponse(System.Net.HttpStatusCode.Forbidden, false, message);
        private async Task<ApiResponse?> AuthorizeReadAsync(int warehouseId)
        {
            var user = _claimService.GetUserClaim();
            var warehouse = await _uow.Warehouses.GetByIdAsync(warehouseId);
            if (warehouse == null) return new ApiResponse().SetNotFound(message: "Warehouse not found.");
            return IsAdmin(user) || (IsWarehouseManager(user) && warehouse.ManagerId == user.Id)
                ? null
                : Forbidden("You do not manage this warehouse.");
        }
        private static bool IsAdmin(ClaimDTO user) => string.Equals(user.Role, Role.ADMIN.ToString(), StringComparison.OrdinalIgnoreCase);
        private static bool IsWarehouseManager(ClaimDTO user) => string.Equals(user.Role, Role.WAREHOUSE_MANAGER.ToString(), StringComparison.OrdinalIgnoreCase);
        private static bool RowVersionMatches(byte[] current, string supplied)
        {
            if (current.Length == 0) return true;
            if (string.IsNullOrWhiteSpace(supplied)) return false;
            try { return current.AsSpan().SequenceEqual(Convert.FromBase64String(supplied)); }
            catch (FormatException) { return false; }
        }
    }
}
