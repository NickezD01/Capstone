using AutoMapper;
using cpms_Application.Interfaces;
using cpms_Application.Request.Supplier;
using cpms_Application.Response;
using cpms_Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Application.Services
{
    public class SupplierService : ISupplierService
    {
        private readonly IUnitOfWork _uow;
        private readonly IMapper _mapper;

        public SupplierService(IUnitOfWork uow, IMapper mapper)
        {
            _uow = uow;
            _mapper = mapper;
        }

        public async Task<ApiResponse> CreateSupplierAsync(CreateSupplierRequest request)
        {
            var supplier = _mapper.Map<Supplier>(request);
            await _uow.Suppliers.AddAsync(supplier);
            await _uow.SaveChangeAsync();
            return new ApiResponse().SetOk("Tạo nhà cung cấp thành công.");
        }

        public async Task<ApiResponse> GetAllSuppliersAsync()
        {
            var suppliers = await _uow.Suppliers.GetAllAsync(null);
            return new ApiResponse().SetOk(suppliers);
        }
    }
}
