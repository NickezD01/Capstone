using AutoMapper;
using cpms_Application.Interfaces;
using cpms_Application.Request.Material;
using cpms_Application.Response;
using cpms_Application.Response.Material;
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
            var material = await _uow.Materials.GetByIdAsync(id);
            if (material == null) return new ApiResponse().SetNotFound(message: $"Material with ID {id} not found.");
            if (string.IsNullOrWhiteSpace(request.MaterialName) || string.IsNullOrWhiteSpace(request.DefaultUnit))
                return new ApiResponse().SetBadRequest(message: "MaterialName and DefaultUnit are required.");

            _mapper.Map(request, material);
            material.MaterialName = material.MaterialName.Trim();
            material.DefaultUnit = material.DefaultUnit.Trim();
            material.ModifiedDate = DateTime.UtcNow;
            await _uow.SaveChangeAsync();
            return new ApiResponse().SetOk(_mapper.Map<MaterialResponse>(material));
        }

        public async Task<ApiResponse> DeleteMaterialAsync(int id)
        {
            var material = await _uow.Materials.GetAsync(m => m.MaterialId == id, q => q.Include(m => m.Variants));
            if (material == null) return new ApiResponse().SetNotFound(message: $"Material with ID {id} not found.");
            material.IsActive = false;
            material.IsDeleted = true;
            material.ModifiedDate = DateTime.UtcNow;
            foreach (var variant in material.Variants)
            {
                variant.IsActive = false;
                variant.IsDeleted = true;
                variant.ModifiedDate = DateTime.UtcNow;
            }
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
            if (!string.IsNullOrWhiteSpace(request.SKU) && await _uow.MaterialVariants.GetAsync(v => v.SKU == request.SKU) != null)
                return new ApiResponse().SetConflict(message: "SKU already exists.");

            var variant = _mapper.Map<MaterialVariant>(request);
            variant.VariantName = variant.VariantName.Trim();
            variant.Unit = variant.Unit.Trim();
            await _uow.MaterialVariants.AddAsync(variant);
            await _uow.SaveChangeAsync();
            variant = await _uow.MaterialVariants.GetAsync(v => v.VariantId == variant.VariantId, q => q.Include(v => v.Material));
            return new ApiResponse().SetOk(_mapper.Map<MaterialVariantResponse>(variant));
        }

        public async Task<ApiResponse> GetVariantsByMaterialAsync(int materialId)
        {
            var variants = await _uow.MaterialVariants.GetAllAsync(v => v.MaterialId == materialId, q => q.Include(v => v.Material));
            return new ApiResponse().SetOk(_mapper.Map<List<MaterialVariantResponse>>(variants));
        }

        public async Task<ApiResponse> UpdateVariantAsync(int variantId, MaterialVariantRequest request)
        {
            var variant = await _uow.MaterialVariants.GetByIdAsync(variantId);
            if (variant == null) return new ApiResponse().SetNotFound(message: "Material variant not found.");
            if (request.MaterialId != variant.MaterialId)
                return new ApiResponse().SetBadRequest(message: "A variant cannot be moved to another material.");
            var duplicate = !string.IsNullOrWhiteSpace(request.SKU)
                ? await _uow.MaterialVariants.GetAsync(v => v.SKU == request.SKU && v.VariantId != variantId)
                : null;
            if (duplicate != null) return new ApiResponse().SetConflict(message: "SKU already exists.");

            _mapper.Map(request, variant);
            variant.ModifiedDate = DateTime.UtcNow;
            await _uow.SaveChangeAsync();
            return new ApiResponse().SetOk("Material variant updated successfully.");
        }

        public async Task<ApiResponse> DeleteVariantAsync(int variantId)
        {
            var variant = await _uow.MaterialVariants.GetByIdAsync(variantId);
            if (variant == null) return new ApiResponse().SetNotFound(message: "Material variant not found.");
            variant.IsActive = false;
            variant.IsDeleted = true;
            variant.ModifiedDate = DateTime.UtcNow;
            await _uow.SaveChangeAsync();
            return new ApiResponse().SetOk("Material variant deactivated successfully.");
        }
    }
}
