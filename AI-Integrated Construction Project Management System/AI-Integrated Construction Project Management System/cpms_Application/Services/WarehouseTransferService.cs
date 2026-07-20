using cpms_Application.Interfaces;
using cpms_Application.Request.WarehouseTransfer;
using cpms_Application.Response;
using cpms_Application.Response.WarehouseTransfer;
using cpms_Domain;
using cpms_Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace cpms_Application.Services
{
    public class WarehouseTransferService : IWarehouseTransferService
    {
        private readonly IUnitOfWork _uow;
        private readonly IClaimService _claimService;

        public WarehouseTransferService(IUnitOfWork uow, IClaimService claimService)
        {
            _uow = uow;
            _claimService = claimService;
        }

        public async Task<ApiResponse> CreateAsync(CreateWarehouseTransferRequest request)
        {
            var user = _claimService.GetUserClaim();
            if (request.SourceWarehouseId == request.DestinationWarehouseId)
                return BadRequest("Source and destination warehouses must differ.");
            if (request.Items == null || request.Items.Count == 0)
                return BadRequest("At least one transfer item is required.");
            if (request.Items.Any(x => x.VariantId <= 0 || x.Quantity <= 0) ||
                request.Items.Select(x => x.VariantId).Distinct().Count() != request.Items.Count)
                return BadRequest("Transfer variants must be unique and quantities must be positive.");

            var source = await _uow.Warehouses.GetByIdAsync(request.SourceWarehouseId);
            var destination = await _uow.Warehouses.GetByIdAsync(request.DestinationWarehouseId);
            if (source == null || destination == null) return NotFound("Source or destination warehouse was not found.");
            if (!IsManagerOf(user, source)) return Forbidden("Only the source warehouse manager may create this transfer.");

            var variants = new List<MaterialVariant>();
            foreach (var item in request.Items)
            {
                var variant = await _uow.MaterialVariants.GetByIdAsync(item.VariantId);
                if (variant == null || !variant.IsActive || variant.IsDeleted)
                    return BadRequest($"Variant {item.VariantId} does not exist or is inactive.");
                variants.Add(variant);
            }

            var transfer = new WarehouseTransfer
            {
                SourceWarehouseId = request.SourceWarehouseId,
                DestinationWarehouseId = request.DestinationWarehouseId,
                Status = WarehouseTransferStatuses.Requested,
                RequestedByUserId = user.Id,
                RequestedAt = DateTime.UtcNow,
                Note = request.Note,
                CreatedBy = user.Id,
                Items = request.Items.Select(x => new WarehouseTransferItem
                {
                    VariantId = x.VariantId,
                    RequestedQuantity = x.Quantity,
                    CreatedBy = user.Id
                }).ToList()
            };

            try
            {
                await _uow.WarehouseTransfers.AddAsync(transfer);
                await _uow.SaveChangeAsync();
                return await GetByIdAsync(transfer.TransferId);
            }
            catch (DbUpdateException)
            {
                return Conflict("The transfer could not be created because related data changed. Reload and retry.");
            }
        }

        public async Task<ApiResponse> GetAllAsync()
        {
            var user = _claimService.GetUserClaim();
            if (!IsAdmin(user) && !IsWarehouseManager(user)) return Forbidden("Warehouse transfer access is not allowed for this role.");

            var transfers = await _uow.WarehouseTransfers.GetAllAsync(
                IsAdmin(user) ? null : t => t.SourceWarehouse.ManagerId == user.Id || t.DestinationWarehouse.ManagerId == user.Id,
                TransferIncludes());
            return new ApiResponse().SetOk(transfers.OrderByDescending(x => x.RequestedAt).Select(Map).ToList());
        }

        public async Task<ApiResponse> GetByIdAsync(int transferId)
        {
            var user = _claimService.GetUserClaim();
            var transfer = await LoadAsync(transferId);
            if (transfer == null) return NotFound("Warehouse transfer not found.");
            if (!CanRead(user, transfer)) return Forbidden("You do not manage either warehouse in this transfer.");
            return new ApiResponse().SetOk(Map(transfer));
        }

        public Task<ApiResponse> ApproveAsync(int transferId) => MutateAsync(transferId, WarehouseTransferStatuses.Requested,
            async (transfer, user) =>
            {
                if (!IsAdmin(user) && !IsManagerOf(user, transfer.DestinationWarehouse)) return Forbidden("Only the destination warehouse manager or an administrator may approve this transfer.");
                if (!IsAdmin(user) && transfer.RequestedByUserId == user.Id) return Conflict("The transfer creator cannot approve the same transfer.");
                foreach (var item in transfer.Items)
                {
                    var inventory = await _uow.Inventories.GetAsync(x => x.WarehouseId == transfer.SourceWarehouseId && x.VariantId == item.VariantId);
                    if (inventory == null || !InventoryQuantityRules.CanReserve(inventory.QuantityOnHand, inventory.ReservedQuantity, inventory.QuarantineQuantity, item.RequestedQuantity))
                        return Conflict($"Insufficient available source stock for transfer item {item.TransferItemId}.");
                    inventory.ReservedQuantity += item.RequestedQuantity;
                    inventory.UpdatedAt = DateTime.UtcNow;
                    await _uow.TransferInventoryReservations.AddAsync(new TransferInventoryReservation
                    {
                        TransferId = transfer.TransferId,
                        TransferItemId = item.TransferItemId,
                        InventoryId = inventory.InventoryId,
                        Quantity = item.RequestedQuantity,
                        Status = TransferReservationStatuses.Active,
                        CreatedAt = DateTime.UtcNow
                    });
                }
                transfer.Status = WarehouseTransferStatuses.Approved;
                transfer.ApprovedByUserId = user.Id;
                transfer.ApprovedAt = DateTime.UtcNow;
                return null;
            });

        public Task<ApiResponse> RejectAsync(int transferId) => MutateAsync(transferId, WarehouseTransferStatuses.Requested,
            (transfer, user) =>
            {
                if (!IsAdmin(user) && !IsManagerOf(user, transfer.DestinationWarehouse)) return Task.FromResult<ApiResponse?>(Forbidden("Only the destination warehouse manager or an administrator may reject this transfer."));
                if (!IsAdmin(user) && transfer.RequestedByUserId == user.Id) return Task.FromResult<ApiResponse?>(Conflict("The transfer creator cannot reject the same transfer."));
                transfer.Status = WarehouseTransferStatuses.Rejected;
                return Task.FromResult<ApiResponse?>(null);
            });

        public async Task<ApiResponse> ShipAsync(int transferId)
        {
            var user = _claimService.GetUserClaim();
            await _uow.BeginTransactionAsync();
            try
            {
                var transfer = await LoadAsync(transferId);
                if (transfer == null) return await Rollback(NotFound("Warehouse transfer not found."));
                if (!IsManagerOf(user, transfer.SourceWarehouse)) return await Rollback(Forbidden("Only the source warehouse manager may ship this transfer."));
                if (transfer.Status != WarehouseTransferStatuses.Approved) return await Rollback(Conflict("Only an approved transfer can be shipped."));

                var sourceInventory = new Dictionary<int, InventoryRecord>();
                var activeReservations = new Dictionary<int, TransferInventoryReservation>();
                foreach (var item in transfer.Items)
                {
                    var inventory = await _uow.Inventories.GetAsync(x => x.WarehouseId == transfer.SourceWarehouseId && x.VariantId == item.VariantId);
                    if (inventory == null || !InventoryQuantityRules.CanIssue(inventory.QuantityOnHand, inventory.ReservedQuantity, inventory.QuarantineQuantity, item.RequestedQuantity))
                        return await Rollback(Conflict($"Reserved source stock is unavailable for transfer item {item.TransferItemId}."));
                    var reservation = await _uow.TransferInventoryReservations.GetAsync(x =>
                        x.TransferItemId == item.TransferItemId && x.Status == TransferReservationStatuses.Active);
                    if (reservation == null || reservation.TransferId != transfer.TransferId ||
                        reservation.InventoryId != inventory.InventoryId || reservation.Quantity != item.RequestedQuantity)
                        return await Rollback(Conflict($"The active reservation ledger is missing or inconsistent for transfer item {item.TransferItemId}."));
                    sourceInventory[item.VariantId] = inventory;
                    activeReservations[item.TransferItemId] = reservation;
                }

                foreach (var item in transfer.Items)
                {
                    var inventory = sourceInventory[item.VariantId];
                    var before = inventory.QuantityOnHand;
                    inventory.QuantityOnHand -= item.RequestedQuantity;
                    inventory.ReservedQuantity -= item.RequestedQuantity;
                    inventory.UpdatedAt = DateTime.UtcNow;
                    item.ShippedQuantity = item.RequestedQuantity;
                    item.UnitCost = inventory.AverageUnitCost;
                    var reservation = activeReservations[item.TransferItemId];
                    reservation.Status = TransferReservationStatuses.Consumed;
                    reservation.ResolvedAt = DateTime.UtcNow;
                    await _uow.InventoryTransactions.AddAsync(NewTransaction(inventory, InventoryTransactionTypes.TransferOut,
                        -item.RequestedQuantity, before, inventory.QuantityOnHand, transfer.TransferId, user.Id, transfer.Note));
                }

                transfer.Status = WarehouseTransferStatuses.InTransit;
                transfer.ShippedByUserId = user.Id;
                transfer.ShippedAt = DateTime.UtcNow;
                transfer.ModifiedDate = DateTime.UtcNow;
                transfer.ModifiedBy = user.Id;
                await _uow.SaveChangeAsync();
                await _uow.CommitTransactionAsync();
                return await GetByIdAsync(transferId);
            }
            catch (DbUpdateConcurrencyException)
            {
                await _uow.RollbackTransactionAsync();
                return Conflict("Inventory or transfer data changed while shipping. Reload and retry.");
            }
            catch (DbUpdateException)
            {
                await _uow.RollbackTransactionAsync();
                return Conflict("The transfer could not be shipped because inventory changed. Reload and retry.");
            }
            catch (Exception)
            {
                await _uow.RollbackTransactionAsync();
                return InternalError("Unable to ship transfer.");
            }
        }

        public async Task<ApiResponse> ReceiveAsync(int transferId, ReceiveWarehouseTransferRequest? request)
        {
            var user = _claimService.GetUserClaim();
            await _uow.BeginTransactionAsync();
            try
            {
                var transfer = await LoadAsync(transferId);
                if (transfer == null) return await Rollback(NotFound("Warehouse transfer not found."));
                if (!IsManagerOf(user, transfer.DestinationWarehouse)) return await Rollback(Forbidden("Only the destination warehouse manager may receive this transfer."));
                if (transfer.Status != WarehouseTransferStatuses.InTransit) return await Rollback(Conflict("Only an in-transit transfer can be received."));

                var receipts = request?.Items?.Count > 0
                    ? request.Items
                    : transfer.Items.Where(x => x.ReceivedQuantity < x.ShippedQuantity)
                        .Select(x => new ReceiveWarehouseTransferItemRequest
                        {
                            TransferItemId = x.TransferItemId,
                            Quantity = x.ShippedQuantity - x.ReceivedQuantity
                        }).ToList();

                if (receipts.Count == 0 || receipts.Any(x => x.Quantity < 0 || x.DamagedQuantity < 0 || x.LostQuantity < 0 ||
                        x.Quantity + x.DamagedQuantity + x.LostQuantity <= 0) ||
                    receipts.Select(x => x.TransferItemId).Distinct().Count() != receipts.Count)
                    return await Rollback(BadRequest("Receipt items must be unique and quantities must be positive."));

                var validated = new List<(WarehouseTransferItem Item, ReceiveWarehouseTransferItemRequest Receipt)>();
                foreach (var receipt in receipts)
                {
                    var item = transfer.Items.SingleOrDefault(x => x.TransferItemId == receipt.TransferItemId);
                    if (item == null) return await Rollback(BadRequest("Receipt contains an item outside this transfer."));
                    if (item.ReceivedQuantity + item.DamagedQuantity + item.LostQuantity +
                        receipt.Quantity + receipt.DamagedQuantity + receipt.LostQuantity > item.ShippedQuantity)
                        return await Rollback(BadRequest($"Receipt exceeds shipped quantity for item {item.TransferItemId}."));
                    validated.Add((item, receipt));
                }

                var destinationInventory = new Dictionary<int, InventoryRecord>();
                foreach (var entry in validated)
                {
                    if (entry.Receipt.Quantity <= 0) continue;
                    var inventory = await _uow.Inventories.GetAsync(x => x.WarehouseId == transfer.DestinationWarehouseId && x.VariantId == entry.Item.VariantId);
                    if (inventory == null)
                    {
                        inventory = new InventoryRecord
                        {
                            WarehouseId = transfer.DestinationWarehouseId,
                            VariantId = entry.Item.VariantId,
                            UpdatedAt = DateTime.UtcNow,
                            CreatedBy = user.Id
                        };
                        await _uow.Inventories.AddAsync(inventory);
                    }
                    destinationInventory[entry.Item.VariantId] = inventory;
                }
                if (destinationInventory.Values.Any(x => x.InventoryId == 0)) await _uow.SaveChangeAsync();

                foreach (var entry in validated)
                {
                    entry.Item.DamagedQuantity += entry.Receipt.DamagedQuantity;
                    entry.Item.LostQuantity += entry.Receipt.LostQuantity;
                    if (entry.Receipt.Quantity <= 0) continue;
                    var inventory = destinationInventory[entry.Item.VariantId];
                    var before = inventory.QuantityOnHand;
                    var after = before + entry.Receipt.Quantity;
                    inventory.AverageUnitCost = after == 0 ? 0 :
                        ((before * inventory.AverageUnitCost) + (entry.Receipt.Quantity * entry.Item.UnitCost)) / after;
                    inventory.QuantityOnHand = after;
                    inventory.UpdatedAt = DateTime.UtcNow;
                    entry.Item.ReceivedQuantity += entry.Receipt.Quantity;
                    await _uow.InventoryTransactions.AddAsync(NewTransaction(inventory, InventoryTransactionTypes.TransferIn,
                        entry.Receipt.Quantity, before, inventory.QuantityOnHand, transfer.TransferId, user.Id, transfer.Note,
                        entry.Item.UnitCost));
                }

                transfer.ReceivedByUserId = user.Id;
                transfer.ModifiedDate = DateTime.UtcNow;
                transfer.ModifiedBy = user.Id;
                if (transfer.Items.All(x => x.ShippedQuantity > 0 && x.ReceivedQuantity + x.DamagedQuantity + x.LostQuantity == x.ShippedQuantity))
                {
                    transfer.Status = transfer.Items.Any(x => x.DamagedQuantity > 0 || x.LostQuantity > 0)
                        ? WarehouseTransferStatuses.ClosedWithVariance
                        : WarehouseTransferStatuses.Received;
                    transfer.ReceivedAt = DateTime.UtcNow;
                }

                await _uow.SaveChangeAsync();
                await _uow.CommitTransactionAsync();
                return await GetByIdAsync(transferId);
            }
            catch (DbUpdateConcurrencyException)
            {
                await _uow.RollbackTransactionAsync();
                return Conflict("Inventory or transfer data changed while receiving. Reload and retry.");
            }
            catch (DbUpdateException)
            {
                await _uow.RollbackTransactionAsync();
                return Conflict("Destination inventory changed while receiving. Reload and retry.");
            }
            catch (Exception)
            {
                await _uow.RollbackTransactionAsync();
                return InternalError("Unable to receive transfer.");
            }
        }

        public async Task<ApiResponse> CancelAsync(int transferId)
        {
            var user = _claimService.GetUserClaim();
            await _uow.BeginTransactionAsync();
            try
            {
                var transfer = await LoadAsync(transferId);
                if (transfer == null) return await Rollback(NotFound("Warehouse transfer not found."));
                if (!IsManagerOf(user, transfer.SourceWarehouse)) return await Rollback(Forbidden("Only the source warehouse manager may cancel this transfer."));
                if (transfer.Status != WarehouseTransferStatuses.Requested && transfer.Status != WarehouseTransferStatuses.Approved)
                    return await Rollback(Conflict("A transfer can only be cancelled before it is shipped."));
                if (transfer.Status == WarehouseTransferStatuses.Approved)
                {
                    foreach (var item in transfer.Items)
                    {
                        var inventory = await _uow.Inventories.GetAsync(x =>
                            x.WarehouseId == transfer.SourceWarehouseId && x.VariantId == item.VariantId);
                        if (inventory == null || inventory.ReservedQuantity < item.RequestedQuantity)
                            return await Rollback(Conflict("Reserved transfer stock is inconsistent. Reload and retry."));
                        inventory.ReservedQuantity -= item.RequestedQuantity;
                        inventory.UpdatedAt = DateTime.UtcNow;
                        var reservation = await _uow.TransferInventoryReservations.GetAsync(x =>
                            x.TransferItemId == item.TransferItemId && x.Status == TransferReservationStatuses.Active);
                        if (reservation == null)
                            return await Rollback(Conflict("Transfer reservation ledger is missing or already resolved."));
                        reservation.Status = TransferReservationStatuses.Released;
                        reservation.ResolvedAt = DateTime.UtcNow;
                    }
                }
                transfer.Status = WarehouseTransferStatuses.Cancelled;
                transfer.ModifiedDate = DateTime.UtcNow;
                transfer.ModifiedBy = user.Id;
                await _uow.SaveChangeAsync();
                await _uow.CommitTransactionAsync();
                return await GetByIdAsync(transferId);
            }
            catch (DbUpdateConcurrencyException)
            {
                await _uow.RollbackTransactionAsync();
                return Conflict("Transfer data changed while cancelling. Reload and retry.");
            }
            catch (Exception)
            {
                await _uow.RollbackTransactionAsync();
                return InternalError("Unable to cancel transfer.");
            }
        }

        private async Task<ApiResponse> MutateAsync(int transferId, string requiredStatus,
            Func<WarehouseTransfer, ClaimDTO, Task<ApiResponse?>> mutation)
        {
            var user = _claimService.GetUserClaim();
            await _uow.BeginTransactionAsync();
            try
            {
                var transfer = await LoadAsync(transferId);
                if (transfer == null) return await Rollback(NotFound("Warehouse transfer not found."));
                if (transfer.Status != requiredStatus) return await Rollback(Conflict($"Transfer must be {requiredStatus} for this operation."));
                var failure = await mutation(transfer, user);
                if (failure != null) return await Rollback(failure);
                transfer.ModifiedDate = DateTime.UtcNow;
                transfer.ModifiedBy = user.Id;
                await _uow.SaveChangeAsync();
                await _uow.CommitTransactionAsync();
                return await GetByIdAsync(transferId);
            }
            catch (DbUpdateConcurrencyException)
            {
                await _uow.RollbackTransactionAsync();
                return Conflict("Transfer data changed. Reload and retry.");
            }
            catch (Exception)
            {
                await _uow.RollbackTransactionAsync();
                return InternalError("Unable to update transfer.");
            }
        }

        private async Task<WarehouseTransfer?> LoadAsync(int transferId) =>
            await _uow.WarehouseTransfers.GetAsync(x => x.TransferId == transferId, TransferIncludes());

        private static Func<IQueryable<WarehouseTransfer>, Microsoft.EntityFrameworkCore.Query.IIncludableQueryable<WarehouseTransfer, object>> TransferIncludes() =>
            q => q.Include(x => x.SourceWarehouse)
                  .Include(x => x.DestinationWarehouse)
                  .Include(x => x.Items).ThenInclude(x => x.Variant).ThenInclude(x => x.Material);

        private async Task<ApiResponse> Rollback(ApiResponse response)
        {
            await _uow.RollbackTransactionAsync();
            return response;
        }

        private static InventoryTransaction NewTransaction(InventoryRecord inventory, string type, decimal quantity,
            decimal before, decimal after, int transferId, int userId, string? note, decimal? unitCost = null) => new()
            {
                InventoryId = inventory.InventoryId,
                WarehouseId = inventory.WarehouseId,
                VariantId = inventory.VariantId,
                TransactionType = type,
                Quantity = quantity,
                QuantityBefore = before,
                QuantityAfter = after,
                ReferenceId = transferId,
                ReferenceType = "WAREHOUSE_TRANSFER",
                Note = note,
                PerformedByUserId = userId,
                TransactionDate = DateTime.UtcNow
                ,UnitCost = unitCost ?? inventory.AverageUnitCost
                ,TotalValue = Math.Abs(quantity) * (unitCost ?? inventory.AverageUnitCost)
            };

        private static WarehouseTransferResponse Map(WarehouseTransfer transfer) => new()
        {
            TransferId = transfer.TransferId,
            SourceWarehouseId = transfer.SourceWarehouseId,
            SourceWarehouseName = transfer.SourceWarehouse.WarehouseName,
            DestinationWarehouseId = transfer.DestinationWarehouseId,
            DestinationWarehouseName = transfer.DestinationWarehouse.WarehouseName,
            Status = transfer.Status,
            RequestedByUserId = transfer.RequestedByUserId,
            ApprovedByUserId = transfer.ApprovedByUserId,
            ShippedByUserId = transfer.ShippedByUserId,
            ReceivedByUserId = transfer.ReceivedByUserId,
            RequestedAt = transfer.RequestedAt,
            ApprovedAt = transfer.ApprovedAt,
            ShippedAt = transfer.ShippedAt,
            ReceivedAt = transfer.ReceivedAt,
            Note = transfer.Note,
            RowVersion = transfer.RowVersion == null ? string.Empty : Convert.ToBase64String(transfer.RowVersion),
            Items = transfer.Items.Select(x => new WarehouseTransferItemResponse
            {
                TransferItemId = x.TransferItemId,
                VariantId = x.VariantId,
                MaterialId = x.Variant.MaterialId,
                MaterialName = x.Variant.Material.MaterialName,
                VariantName = x.Variant.VariantName,
                Unit = x.Variant.Unit,
                RequestedQuantity = x.RequestedQuantity,
                ShippedQuantity = x.ShippedQuantity,
                ReceivedQuantity = x.ReceivedQuantity
                ,DamagedQuantity = x.DamagedQuantity
                ,LostQuantity = x.LostQuantity
                ,UnitCost = x.UnitCost
            }).ToList()
        };

        private static bool IsAdmin(ClaimDTO user) => string.Equals(user.Role, Role.ADMIN.ToString(), StringComparison.OrdinalIgnoreCase);
        private static bool IsWarehouseManager(ClaimDTO user) => string.Equals(user.Role, Role.WAREHOUSE_MANAGER.ToString(), StringComparison.OrdinalIgnoreCase);
        private static bool IsManagerOf(ClaimDTO user, Warehouse warehouse) => IsWarehouseManager(user) && warehouse.ManagerId == user.Id;
        private static bool CanRead(ClaimDTO user, WarehouseTransfer transfer) => IsAdmin(user) ||
            (IsWarehouseManager(user) && (transfer.SourceWarehouse.ManagerId == user.Id || transfer.DestinationWarehouse.ManagerId == user.Id));
        private static ApiResponse BadRequest(string message) => new ApiResponse().SetBadRequest(message: message);
        private static ApiResponse NotFound(string message) => new ApiResponse().SetNotFound(message: message);
        private static ApiResponse Conflict(string message) => new ApiResponse().SetConflict(message: message);
        private static ApiResponse InternalError(string message) =>
            new ApiResponse().SetApiResponse(System.Net.HttpStatusCode.InternalServerError, false, message);
        private static ApiResponse Forbidden(string message) => new ApiResponse().SetApiResponse(System.Net.HttpStatusCode.Forbidden, false, message);
    }
}
