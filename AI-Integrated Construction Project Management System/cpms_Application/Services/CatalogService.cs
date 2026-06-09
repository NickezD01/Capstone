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
            var existingEntry = await _uow.SupplierCatalogs.GetAsync(x =>
                x.SupplierId == request.SupplierId && x.MaterialId == request.MaterialId);

            if (existingEntry != null)
                return new ApiResponse().SetBadRequest("Vật liệu này đã tồn tại trong danh mục của nhà cung cấp này.");

            var catalog = _mapper.Map<SupplierCatalog>(request);
            await _uow.SupplierCatalogs.AddAsync(catalog);
            await _uow.SaveChangeAsync();
            return new ApiResponse().SetOk("Đã thêm vào danh mục thành công.");
        }
    }
}
