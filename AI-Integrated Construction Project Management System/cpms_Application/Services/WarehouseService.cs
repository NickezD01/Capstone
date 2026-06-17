using AutoMapper;
using cpms_Application.Interfaces;
using cpms_Application.Request.Warehouse;
using cpms_Application.Response;
using cpms_Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Application.Services
{
    public class WarehouseService : IWarehouseService
    {
        private readonly IUnitOfWork _uow;
        private readonly IMapper _mapper;

        public WarehouseService(IUnitOfWork uow, IMapper mapper)
        {
            _uow = uow;
            _mapper = mapper;
        }

        public async Task<ApiResponse> CreateWarehouseAsync(CreateWarehouseRequest request)
        {
            var warehouse = _mapper.Map<Warehouse>(request);
            await _uow.Warehouses.AddAsync(warehouse);
            await _uow.SaveChangeAsync();
            return new ApiResponse().SetOk("Kho đã được tạo thành công!");
        }

        public async Task<ApiResponse> GetAllWarehousesAsync()
        {
            var list = await _uow.Warehouses.GetAllAsync(null);
            return new ApiResponse().SetOk(list);
        }
    }
}
