using AutoMapper;
using cpms_Application.Interfaces;
using cpms_Application.Request.Warehouse;
using cpms_Application.Response;
using cpms_Domain.Models;
using Microsoft.EntityFrameworkCore;
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
        public async Task<ApiResponse> GetWarehouseInventoryAsync(int warehouseId)
        {
            var response = new ApiResponse();
            try
            {
                // Lấy danh sách inventory của kho, include thêm Material để lấy tên
                var inventories = await _uow.Inventories.GetAllAsync(
                    filter: x => x.WarehouseId == warehouseId,
                    include: query => query.Include(i => i.Material)
                );

                // Map sang DTO để hiển thị đẹp hơn
                var result = inventories.Select(i => new
                {
                    MaterialName = i.Material.MaterialName, // Giả sử model Material có property Name
                    Quantity = i.Quantity,
                    Unit = i.Material.Unit // Giả sử có property Unit
                });

                return response.SetOk(result);
            }
            catch (Exception ex)
            {
                return response.SetBadRequest("Lỗi lấy dữ liệu kho: " + ex.Message);
            }
        }
    }
}
