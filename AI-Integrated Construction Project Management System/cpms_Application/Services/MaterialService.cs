using AutoMapper;
using cpms_Application.Interfaces;
using cpms_Application.Request.Material;
using cpms_Application.Response;
using cpms_Application.Response.Material;
using cpms_Domain;
using cpms_Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace cpms_Application.Services
{
    public class MaterialService : IMaterialService
    {
        private readonly IUnitOfWork _uow;
        private readonly IMapper _mapper;

        public MaterialService(IUnitOfWork uow, IMapper mapper)
        {
            _uow = uow;
            _mapper = mapper;
        }

        public async Task<ApiResponse> CreateMaterialAsync(Request.Material.MaterialRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.MaterialName) || string.IsNullOrWhiteSpace(request.DefaultUnit))
                return new ApiResponse().SetBadRequest(message: "MaterialName and DefaultUnit are required.");
            if (await _uow.Categories.GetByIdAsync(request.CategoryId) == null)
                return new ApiResponse().SetBadRequest(message: "Category does not exist.");

            var material = _mapper.Map<Material>(request);
            material.MaterialName = material.MaterialName.Trim();
            material.DefaultUnit = material.DefaultUnit.Trim();
            await _uow.Materials.AddAsync(material);
            await _uow.SaveChangeAsync();
            return new ApiResponse().SetOk(_mapper.Map<MaterialResponse>(material));
        }

        public async Task<ApiResponse> GetAllMaterialsAsync()
        {
            var data = await _uow.Materials.GetAllAsync(null, q => q.Include(m => m.Variants));
            return new ApiResponse().SetOk(_mapper.Map<List<MaterialResponse>>(data));
        }

        public async Task<ApiResponse> GetMaterialByIdAsync(int id)
        {
            var material = await _uow.Materials.GetAsync(m => m.MaterialId == id, q => q.Include(m => m.Variants));
            return material == null
                ? new ApiResponse().SetNotFound(message: $"Material with ID {id} not found.")
                : new ApiResponse().SetOk(_mapper.Map<MaterialResponse>(material));
        }

        public async Task<ApiResponse> UpdateMaterialAsync(int id, UpdateMaterialRequest request)
        {
            var material = await _uow.Materials.GetAsync(m => m.MaterialId == id, q => q.Include(m => m.Variants));
            if (material == null) return new ApiResponse().SetNotFound(message: $"Material with ID {id} not found.");
            if (string.IsNullOrWhiteSpace(request.MaterialName) || string.IsNullOrWhiteSpace(request.DefaultUnit))
                return new ApiResponse().SetBadRequest(message: "MaterialName and DefaultUnit are required.");

            if (material.IsActive && !request.IsActive)
            {
                foreach (var variant in material.Variants)
                {
                    var conflict = await GetVariantDeactivationConflictAsync(variant.VariantId);
                    if (conflict != null)
                        return new ApiResponse().SetConflict(message: conflict);
                }
            }

            _mapper.Map(request, material);
            material.MaterialName = material.MaterialName.Trim();
            material.DefaultUnit = material.DefaultUnit.Trim();
            material.ModifiedDate = DateTime.UtcNow;
            if (!material.IsActive)
            {
                foreach (var variant in material.Variants)
                    await DeactivateVariantAsync(variant);
            }
            await _uow.SaveChangeAsync();
            return new ApiResponse().SetOk(_mapper.Map<MaterialResponse>(material));
        }

        public async Task<ApiResponse> DeleteMaterialAsync(int id)
        {
            var material = await _uow.Materials.GetAsync(m => m.MaterialId == id, q => q.Include(m => m.Variants));
            if (material == null) return new ApiResponse().SetNotFound(message: $"Material with ID {id} not found.");
            foreach (var variant in material.Variants)
            {
                var conflict = await GetVariantDeactivationConflictAsync(variant.VariantId);
                if (conflict != null)
                    return new ApiResponse().SetConflict(message: conflict);
            }
            material.IsActive = false;
            material.ModifiedDate = DateTime.UtcNow;
            foreach (var variant in material.Variants)
                await DeactivateVariantAsync(variant);
            await _uow.SaveChangeAsync();
            return new ApiResponse().SetOk("Material deactivated successfully.");
        }

        public async Task<ApiResponse> CreateVariantAsync(MaterialVariantRequest request)
        {
            var material = await _uow.Materials.GetByIdAsync(request.MaterialId);
            if (material == null || !material.IsActive)
                return new ApiResponse().SetBadRequest(message: "Material does not exist.");
            if (string.IsNullOrWhiteSpace(request.VariantName) || string.IsNullOrWhiteSpace(request.Unit))
                return new ApiResponse().SetBadRequest(message: "VariantName and Unit are required.");
            await _uow.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                var normalizedSku = MaterialSkuRules.Normalize(request.SKU);
                if (normalizedSku != null && await _uow.MaterialVariants.GetAsync(v => v.SKU == normalizedSku) != null)
                {
                    await _uow.RollbackTransactionAsync();
                    return new ApiResponse().SetConflict(message: "SKU already exists.");
                }

                var variant = _mapper.Map<MaterialVariant>(request);
                variant.Material = material;
                variant.VariantName = variant.VariantName.Trim();
                variant.Unit = variant.Unit.Trim();
                variant.SKU = normalizedSku;
                await _uow.MaterialVariants.AddAsync(variant);
                await _uow.SaveChangeAsync();

                if (variant.SKU == null)
                {
                    variant.SKU = await GenerateAvailableSkuAsync(variant.MaterialId, variant.VariantId);
                    await _uow.SaveChangeAsync();
                }

                await _uow.CommitTransactionAsync();
                variant = await _uow.MaterialVariants.GetAsync(v => v.VariantId == variant.VariantId, q => q.Include(v => v.Material));
                return new ApiResponse().SetOk(_mapper.Map<MaterialVariantResponse>(variant));
            }
            catch (DbUpdateException)
            {
                await _uow.RollbackTransactionAsync();
                return new ApiResponse().SetConflict(message: "SKU already exists.");
            }
            catch (Exception)
            {
                await _uow.RollbackTransactionAsync();
                return new ApiResponse().SetApiResponse(System.Net.HttpStatusCode.InternalServerError, false, "Unable to create material variant.");
            }
        }

        public async Task<ApiResponse> GetVariantsByMaterialAsync(int materialId)
        {
            var variants = await _uow.MaterialVariants.GetAllAsync(v => v.MaterialId == materialId, q => q.Include(v => v.Material));
            return new ApiResponse().SetOk(_mapper.Map<List<MaterialVariantResponse>>(variants));
        }

        public async Task<ApiResponse> GetVariantByIdAsync(int variantId)
        {
            var variant = await _uow.MaterialVariants.GetAsync(v => v.VariantId == variantId,
                q => q.Include(v => v.Material));
            return variant == null
                ? new ApiResponse().SetNotFound("Material variant not found.")
                : new ApiResponse().SetOk(_mapper.Map<MaterialVariantResponse>(variant));
        }

        public async Task<ApiResponse> UpdateVariantAsync(int variantId, MaterialVariantRequest request)
        {
            var variant = await _uow.MaterialVariants.GetByIdAsync(variantId);
            if (variant == null) return new ApiResponse().SetNotFound(message: "Material variant not found.");
            if (request.MaterialId != variant.MaterialId)
                return new ApiResponse().SetBadRequest(message: "A variant cannot be moved to another material.");
            var material = await _uow.Materials.GetByIdAsync(variant.MaterialId);
            if (request.IsActive && (material == null || !material.IsActive))
                return new ApiResponse().SetConflict(message: "A variant cannot be activated while its material is inactive.");
            if (variant.IsActive && !request.IsActive)
            {
                var conflict = await GetVariantDeactivationConflictAsync(variantId);
                if (conflict != null) return new ApiResponse().SetConflict(message: conflict);
            }
            var requestedSku = MaterialSkuRules.Normalize(request.SKU);
            var currentSku = MaterialSkuRules.Normalize(variant.SKU);
            var normalizedSku = requestedSku ?? currentSku ?? await GenerateAvailableSkuAsync(variant.MaterialId, variant.VariantId);
            var duplicate = await _uow.MaterialVariants.GetAsync(v => v.SKU == normalizedSku && v.VariantId != variantId);
            if (duplicate != null) return new ApiResponse().SetConflict(message: "SKU already exists.");
            if (currentSku != null && normalizedSku != currentSku && await HasOperationalUseAsync(variantId))
                return new ApiResponse().SetConflict(message:
                    $"SKU {currentSku} is already used by planning, purchasing, or inventory records and cannot be changed. Create a new variant if the stock identity is different.");

            _mapper.Map(request, variant);
            variant.VariantName = variant.VariantName.Trim();
            variant.Unit = variant.Unit.Trim();
            variant.SKU = normalizedSku;
            variant.ModifiedDate = DateTime.UtcNow;
            if (!variant.IsActive)
                await DisableCatalogOffersAsync(variant.VariantId);
            try
            {
                await _uow.SaveChangeAsync();
            }
            catch (DbUpdateException)
            {
                return new ApiResponse().SetConflict(message: "SKU already exists.");
            }
            variant = await _uow.MaterialVariants.GetAsync(v => v.VariantId == variantId,
                q => q.Include(v => v.Material));
            return new ApiResponse().SetOk(_mapper.Map<MaterialVariantResponse>(variant));
        }

        private async Task<string> GenerateAvailableSkuAsync(int materialId, int variantId)
        {
            var baseSku = MaterialSkuRules.Generate(materialId, variantId);
            var candidate = baseSku;
            var suffix = 2;
            while (await _uow.MaterialVariants.GetAsync(v => v.SKU == candidate && v.VariantId != variantId) != null)
                candidate = $"{baseSku}-{suffix++}";
            return candidate;
        }

        private async Task<bool> HasOperationalUseAsync(int variantId) =>
            await _uow.Inventories.GetAsync(i => i.VariantId == variantId) != null ||
            await _uow.OrderLineItems.GetAsync(i => i.VariantId == variantId) != null ||
            await _uow.MaterialRequisitions.GetAsync(i => i.VariantId == variantId) != null ||
            await _uow.TaskMaterialRequirements.GetAsync(i => i.VariantId == variantId) != null;

        public async Task<ApiResponse> DeleteVariantAsync(int variantId)
        {
            var variant = await _uow.MaterialVariants.GetByIdAsync(variantId);
            if (variant == null) return new ApiResponse().SetNotFound(message: "Material variant not found.");
            var conflict = await GetVariantDeactivationConflictAsync(variantId);
            if (conflict != null) return new ApiResponse().SetConflict(message: conflict);
            await DeactivateVariantAsync(variant);
            await _uow.SaveChangeAsync();
            return new ApiResponse().SetOk("Material variant deactivated successfully.");
        }

        private async Task DeactivateVariantAsync(MaterialVariant variant)
        {
            variant.IsActive = false;
            variant.ModifiedDate = DateTime.UtcNow;
            await DisableCatalogOffersAsync(variant.VariantId);
        }

        private async Task DisableCatalogOffersAsync(int variantId)
        {
            var catalogs = await _uow.SupplierCatalogs.GetAllAsync(c => c.VariantId == variantId && c.IsAvailable);
            foreach (var catalog in catalogs)
            {
                catalog.IsAvailable = false;
                catalog.ModifiedDate = DateTime.UtcNow;
            }
        }

        private async Task<string?> GetVariantDeactivationConflictAsync(int variantId)
        {
            var balances = await _uow.Inventories.GetAllAsync(i => i.VariantId == variantId);
            if (balances.Any(i => i.QuantityOnHand != 0 || i.ReservedQuantity != 0 ||
                                  i.OnOrderQuantity != 0 || i.QuarantineQuantity != 0))
                return $"Variant {variantId} cannot be deactivated while warehouse stock, reservations, on-order quantity, or quarantine quantity remains.";

            var openOrderLine = await _uow.OrderLineItems.GetAsync(line =>
                line.VariantId == variantId &&
                (line.PurchaseOrder.Status == PurchaseOrderStatus.PENDING ||
                 line.PurchaseOrder.Status == PurchaseOrderStatus.APPROVED ||
                 line.PurchaseOrder.Status == PurchaseOrderStatus.PROCESSING ||
                 line.PurchaseOrder.Status == PurchaseOrderStatus.SHIPPED ||
                 line.PurchaseOrder.Status == PurchaseOrderStatus.PARTIALLY_RECEIVED));
            if (openOrderLine != null)
                return $"Variant {variantId} cannot be deactivated while an open purchase order references it.";

            var activeRequestItem = await _uow.MaterialRequisitions.GetAsync(item =>
                item.VariantId == variantId &&
                (item.MaterialRequest.Status == MaterialRequestStatuses.Pending ||
                 item.MaterialRequest.Status == MaterialRequestStatuses.Approved ||
                 item.MaterialRequest.Status == MaterialRequestStatuses.PartiallyApproved ||
                 item.MaterialRequest.Status == MaterialRequestStatuses.PartiallyIssued));
            if (activeRequestItem != null)
                return $"Variant {variantId} cannot be deactivated while an active material request still needs it.";

            var activeTransferItem = await _uow.WarehouseTransferItems.GetAsync(item =>
                item.VariantId == variantId &&
                (item.Transfer.Status == WarehouseTransferStatuses.Requested ||
                 item.Transfer.Status == WarehouseTransferStatuses.Approved ||
                 item.Transfer.Status == WarehouseTransferStatuses.InTransit));
            if (activeTransferItem != null)
                return $"Variant {variantId} cannot be deactivated while a warehouse transfer is open.";

            var activeTaskPlan = await _uow.TaskMaterialRequirements.GetAsync(requirement =>
                requirement.VariantId == variantId &&
                requirement.TaskItem.Status != cpms_Domain.Models.TaskStatus.COMPLETED &&
                requirement.TaskItem.Status != cpms_Domain.Models.TaskStatus.CANCELLED &&
                requirement.TaskItem.Status != cpms_Domain.Models.TaskStatus.REJECTED);
            if (activeTaskPlan != null)
                return $"Variant {variantId} cannot be deactivated while an active task material plan references it.";

            return null;
        }
    }
}
