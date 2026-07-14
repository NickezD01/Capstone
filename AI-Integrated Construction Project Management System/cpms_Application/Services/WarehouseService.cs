using AutoMapper;
using cpms_Application.Interfaces;
using cpms_Application.Request.Warehouse;
using cpms_Application.Response;
using cpms_Application.Response.Inventory;
using cpms_Application.Response.Warehouse;
using cpms_Domain.Models;
using cpms_Domain;
using Microsoft.EntityFrameworkCore;

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
            if (await _uow.UserAccounts.GetByIdAsync(warehouse.ManagerId) == null)
                return new ApiResponse().SetBadRequest(message: "Warehouse manager does not exist.");
            await _uow.Warehouses.AddAsync(warehouse);
            await _uow.SaveChangeAsync();
            return new ApiResponse().SetOk("Warehouse created successfully.");
        }

        public async Task<ApiResponse> GetAllWarehousesAsync()
        {
            var list = await _uow.Warehouses.GetAllAsync(null,
                q => q.Include(w => w.Manager).Include(w => w.InventoryRecords));
            return new ApiResponse().SetOk(_mapper.Map<List<WarehouseResponse>>(list));
        }

        public async Task<ApiResponse> GetWarehouseInventoryAsync(int warehouseId)
        {
            var records = await _uow.Inventories.GetAllAsync(i => i.WarehouseId == warehouseId,
                q => q.Include(i => i.Warehouse).Include(i => i.Variant).ThenInclude(v => v.Material));
            return new ApiResponse().SetOk(_mapper.Map<List<InventoryReportResponse>>(records));
        }

        public async Task<ApiResponse> GetInventoryAsync(int warehouseId, int variantId)
        {
            var record = await _uow.Inventories.GetAsync(i => i.WarehouseId == warehouseId && i.VariantId == variantId,
                q => q.Include(i => i.Warehouse).Include(i => i.Variant).ThenInclude(v => v.Material));
            return record == null ? new ApiResponse().SetNotFound(message: "Inventory record not found.")
                : new ApiResponse().SetOk(_mapper.Map<InventoryReportResponse>(record));
        }

        public async Task<ApiResponse> AdjustInventoryAsync(InventoryAdjustmentRequest request)
        {
            var user = _claimService.GetUserClaim();
            if (!string.Equals(user.Role, Role.WAREHOUSE_MANAGER.ToString(), StringComparison.OrdinalIgnoreCase)) return Forbidden("Only warehouse managers may adjust inventory.");
            if (request.QuantityDelta == 0) return new ApiResponse().SetBadRequest(message: "QuantityDelta cannot be zero.");
            await _uow.BeginTransactionAsync();
            try
            {
                var inventory = await _uow.Inventories.GetAsync(i => i.WarehouseId == request.WarehouseId && i.VariantId == request.VariantId);
                if (inventory == null)
                {
                    if (request.QuantityDelta < 0) { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetBadRequest(message: "Cannot create inventory with a negative adjustment."); }
                    if (await _uow.Warehouses.GetByIdAsync(request.WarehouseId) == null || await _uow.MaterialVariants.GetByIdAsync(request.VariantId) == null)
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
                else if (!string.IsNullOrWhiteSpace(request.RowVersion) &&
                    !Convert.ToBase64String(inventory.RowVersion).Equals(request.RowVersion, StringComparison.Ordinal))
                {
                    await _uow.RollbackTransactionAsync();
                    return new ApiResponse().SetConflict(message: "Inventory has changed. Reload and retry.");
                }

                var before = inventory.QuantityOnHand;
                var after = before + request.QuantityDelta;
                if (!InventoryQuantityRules.CanAdjust(before, inventory.ReservedQuantity, request.QuantityDelta))
                { await _uow.RollbackTransactionAsync(); return new ApiResponse().SetConflict(message: "Adjustment cannot reduce stock below the reserved quantity."); }
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
                return new ApiResponse().SetConflict(message: "Inventory has changed. Reload and retry.");
            }
            catch (Exception ex)
            {
                await _uow.RollbackTransactionAsync();
                return new ApiResponse().SetBadRequest(message: "Unable to adjust inventory: " + ex.Message);
            }
        }

        public async Task<ApiResponse> GetTransactionsAsync(int? warehouseId, int? variantId)
        {
            var transactions = await _uow.InventoryTransactions.GetAllAsync(t =>
                (!warehouseId.HasValue || t.WarehouseId == warehouseId.Value) &&
                (!variantId.HasValue || t.VariantId == variantId.Value));
            return new ApiResponse().SetOk(_mapper.Map<List<InventoryTransactionResponse>>(transactions.OrderByDescending(t => t.TransactionDate)));
        }

        private static ApiResponse Forbidden(string message) => new ApiResponse().SetApiResponse(System.Net.HttpStatusCode.Forbidden, false, message);
    }
}
