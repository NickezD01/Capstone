using AutoMapper;
using cpms_Application.Interfaces;
using cpms_Application.Request.SupplierCatalog;
using cpms_Application.Response;
using cpms_Domain.Models;
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
            if (await _uow.MaterialVariants.GetByIdAsync(variantId) == null)
                return new ApiResponse().SetBadRequest(message: "Material variant does not exist.");
            if (await _uow.Suppliers.GetByIdAsync(request.SupplierId) == null)
                return new ApiResponse().SetBadRequest(message: "Supplier does not exist.");
            if (request.UnitPrice < 0 || request.MinimumOrderQuantity < 0 || request.LeadTimeDays < 0)
                return new ApiResponse().SetBadRequest(message: "Price, minimum quantity, and lead time cannot be negative.");
            var existingEntry = await _uow.SupplierCatalogs.GetAsync(x =>
                x.SupplierId == request.SupplierId && x.VariantId == variantId);

            if (existingEntry != null)
                return new ApiResponse().SetBadRequest("Vật liệu này đã tồn tại trong danh mục của nhà cung cấp này.");

            var catalog = _mapper.Map<SupplierCatalog>(request);
            catalog.VariantId = variantId;
            await _uow.SupplierCatalogs.AddAsync(catalog);
            await _uow.SaveChangeAsync();
            return new ApiResponse().SetOk("Đã thêm vào danh mục thành công.");
        }
    }
}
