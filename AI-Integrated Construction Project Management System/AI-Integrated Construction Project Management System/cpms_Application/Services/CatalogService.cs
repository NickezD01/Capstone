using AutoMapper;
using cpms_Application.Interfaces;
using cpms_Application.Request.SupplierCatalog;
using cpms_Application.Response;
using cpms_Application.Response.SupplierCatalog;
using cpms_Domain.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Application.Services
{
    public class CatalogService : ICatalogService
    {
        private readonly IUnitOfWork _uow;
        private readonly IMapper _mapper;

        public CatalogService(IUnitOfWork uow, IMapper mapper)
        {
            _uow = uow;
            _mapper = mapper;
        }

        public async Task<ApiResponse> AddMaterialToCatalogAsync(CreateCatalogRequest request)
        {
            // Kiểm tra xem đã có cặp Supplier-Material này chưa để tránh trùng lặp
            var variantId = request.VariantId;
            if (variantId == 0)
            {
                var activeVariants = await _uow.MaterialVariants.GetAllAsync(v => v.MaterialId == request.MaterialId && v.IsActive);
                if (activeVariants.Count != 1)
                    return new ApiResponse().SetBadRequest(message: "MaterialId must resolve to exactly one active variant; otherwise VariantId is required.");
                variantId = activeVariants[0].VariantId;
            }
            var variant = await _uow.MaterialVariants.GetByIdAsync(variantId);
            if (variant == null || !variant.IsActive)
                return new ApiResponse().SetBadRequest(message: "Material variant does not exist or is inactive.");
            var supplier = await _uow.Suppliers.GetByIdAsync(request.SupplierId);
            if (supplier == null || supplier.IsDeleted)
                return new ApiResponse().SetBadRequest(message: "Supplier does not exist.");
            if (request.UnitPrice < 0 || request.MinimumOrderQuantity < 0 || request.LeadTimeDays < 0)
                return new ApiResponse().SetBadRequest(message: "Price, minimum quantity, and lead time cannot be negative.");
            if (request.IsAvailable && request.UnitPrice <= 0)
                return new ApiResponse().SetBadRequest(message: "An available supplier offer must have a positive unit price.");
            var existingEntry = await _uow.SupplierCatalogs.GetAsync(x =>
                x.SupplierId == request.SupplierId && x.VariantId == variantId);

            if (existingEntry != null)
                return new ApiResponse().SetConflict("This supplier already has a catalog offer for the selected material variant.");

            var catalog = _mapper.Map<SupplierCatalog>(request);
            catalog.VariantId = variantId;
            catalog.Supplier = supplier;
            catalog.Variant = variant;
            catalog.SupplierSku = string.IsNullOrWhiteSpace(request.SupplierSku) ? null : request.SupplierSku.Trim();
            await _uow.SupplierCatalogs.AddAsync(catalog);
            await _uow.SaveChangeAsync();
            return new ApiResponse().SetApiResponse(System.Net.HttpStatusCode.Created, true,
                result: await MapOfferAsync(catalog.CatalogId));
        }

        public async Task<ApiResponse> GetCatalogOffersAsync(int? supplierId, int? variantId, bool availableOnly = true)
        {
            if (supplierId <= 0 || variantId <= 0)
                return new ApiResponse().SetBadRequest(message: "SupplierId and VariantId filters must be positive when supplied.");

            var catalogs = await _uow.SupplierCatalogs.GetAllAsync(c =>
                    (!supplierId.HasValue || c.SupplierId == supplierId.Value) &&
                    (!variantId.HasValue || c.VariantId == variantId.Value) &&
                    !c.Supplier.IsDeleted &&
                    (!availableOnly || (c.IsAvailable && c.Variant.IsActive && c.Variant.Material.IsActive)),
                q => q.Include(c => c.Supplier)
                      .Include(c => c.Variant).ThenInclude(v => v.Material));

            var result = catalogs
                .OrderBy(c => c.Supplier.CompanyName)
                .ThenBy(c => c.Variant.Material.MaterialName)
                .ThenBy(c => c.Variant.VariantName)
                .Select(c => new CatalogOfferResponse
                {
                    CatalogId = c.CatalogId,
                    SupplierId = c.SupplierId,
                    SupplierName = c.Supplier.CompanyName,
                    VariantId = c.VariantId,
                    MaterialId = c.Variant.MaterialId,
                    MaterialName = c.Variant.Material.MaterialName,
                    VariantName = c.Variant.VariantName,
                    Sku = c.Variant.SKU,
                    SupplierSku = c.SupplierSku,
                    Unit = c.Variant.Unit,
                    UnitPrice = c.UnitPrice,
                    MinimumOrderQuantity = c.MinimumOrderQuantity,
                    LeadTimeDays = c.LeadTimeDays,
                    IsAvailable = c.IsAvailable
                })
                .ToList();

            return new ApiResponse().SetOk(result);
        }

        public async Task<ApiResponse> GetCatalogOfferByIdAsync(int catalogId)
        {
            var result = await MapOfferAsync(catalogId);
            return result == null
                ? new ApiResponse().SetNotFound("Supplier catalog offer not found.")
                : new ApiResponse().SetOk(result);
        }

        public async Task<ApiResponse> UpdateCatalogOfferAsync(int catalogId, UpdateCatalogRequest request)
        {
            var catalog = await _uow.SupplierCatalogs.GetByIdAsync(catalogId);
            if (catalog == null) return new ApiResponse().SetNotFound("Supplier catalog offer not found.");
            if (request.UnitPrice < 0 || request.MinimumOrderQuantity < 0 || request.LeadTimeDays < 0)
                return new ApiResponse().SetBadRequest("Price, minimum quantity, and lead time cannot be negative.");
            if (request.IsAvailable && request.UnitPrice <= 0)
                return new ApiResponse().SetBadRequest("An available supplier offer must have a positive unit price.");
            var supplier = await _uow.Suppliers.GetByIdAsync(catalog.SupplierId);
            var variant = await _uow.MaterialVariants.GetByIdAsync(catalog.VariantId);
            var material = variant == null ? null : await _uow.Materials.GetByIdAsync(variant.MaterialId);
            if (request.IsAvailable && (supplier == null || supplier.IsDeleted || variant == null || !variant.IsActive || material == null || !material.IsActive))
                return new ApiResponse().SetConflict("The offer cannot be activated while its supplier, material, or variant is inactive.");
            catalog.SupplierSku = string.IsNullOrWhiteSpace(request.SupplierSku) ? null : request.SupplierSku.Trim();
            catalog.UnitPrice = request.UnitPrice;
            catalog.MinimumOrderQuantity = request.MinimumOrderQuantity;
            catalog.LeadTimeDays = request.LeadTimeDays;
            catalog.IsAvailable = request.IsAvailable;
            catalog.ModifiedDate = DateTime.UtcNow;
            await _uow.SaveChangeAsync();
            return new ApiResponse().SetOk(await MapOfferAsync(catalogId));
        }

        public async Task<ApiResponse> DeactivateCatalogOfferAsync(int catalogId)
        {
            var catalog = await _uow.SupplierCatalogs.GetByIdAsync(catalogId);
            if (catalog == null) return new ApiResponse().SetNotFound("Supplier catalog offer not found.");
            catalog.IsAvailable = false;
            catalog.ModifiedDate = DateTime.UtcNow;
            await _uow.SaveChangeAsync();
            return new ApiResponse().SetOk("Supplier catalog offer deactivated.");
        }

        private async Task<CatalogOfferResponse?> MapOfferAsync(int catalogId)
        {
            var catalog = await _uow.SupplierCatalogs.GetAsync(c => c.CatalogId == catalogId,
                q => q.Include(c => c.Supplier).Include(c => c.Variant).ThenInclude(v => v.Material));
            if (catalog == null || catalog.Supplier.IsDeleted) return null;
            return new CatalogOfferResponse
            {
                CatalogId = catalog.CatalogId,
                SupplierId = catalog.SupplierId,
                SupplierName = catalog.Supplier.CompanyName,
                VariantId = catalog.VariantId,
                MaterialId = catalog.Variant.MaterialId,
                MaterialName = catalog.Variant.Material.MaterialName,
                VariantName = catalog.Variant.VariantName,
                Sku = catalog.Variant.SKU,
                SupplierSku = catalog.SupplierSku,
                Unit = catalog.Variant.Unit,
                UnitPrice = catalog.UnitPrice,
                MinimumOrderQuantity = catalog.MinimumOrderQuantity,
                LeadTimeDays = catalog.LeadTimeDays,
                IsAvailable = catalog.IsAvailable
            };
        }
    }
}
