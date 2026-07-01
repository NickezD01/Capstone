using AutoMapper;
using cpms_Application.Interfaces;
using cpms_Application.Request.Warehouse;
using cpms_Application.Response;
using cpms_Application.Response.Inventory;
using cpms_Application.Response.Warehouse;
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
        private readonly IClaimService _claimService;

        public WarehouseService(IUnitOfWork uow, IMapper mapper, IClaimService claimService)
        {
            _uow = uow;
            _mapper = mapper;
            _claimService = claimService;
            }

        public async Task<ApiResponse> CreateWarehouseAsync(CreateWarehouseRequest request)
        {
            var response = new ApiResponse();
            try
            {
                // 3. Map dữ liệu từ request sang thực thể Kho
                var warehouse = _mapper.Map<Warehouse>(request);

                // 🚀 4. TỰ ĐỘNG LẤY ID NGƯỜI TẠO TỪ TOKEN JWT
                var userClaim = _claimService.GetUserClaim();
                warehouse.ManagerId = userClaim.Id;

                // 5. Lưu vào Database
                await _uow.Warehouses.AddAsync(warehouse);
                await _uow.SaveChangeAsync();

                return response.SetOk("Kho đã được tạo thành công với người quản lý là bạn!");
            }
            catch (Exception ex)
            {
                // Giữ lại bộ đào lỗi 3 tầng để bắt các lỗi database khác (nếu có)
                var errorMsg = ex.InnerException?.InnerException != null
                    ? ex.InnerException.InnerException.Message
                    : (ex.InnerException != null ? ex.InnerException.Message : ex.Message);

                return response.SetBadRequest("Lỗi lưu dữ liệu kho: " + errorMsg);
            }
        }

        public async Task<ApiResponse> GetAllWarehousesAsync()
        {
            var response = new ApiResponse();
            try
            {
                var list = await _uow.Warehouses.GetAllAsync(
                    filter: null,
                    include: query => query.Include(w => w.Manager).Include(w => w.InventoryRecords)
                );

                var result = list.Select(w => new WarehouseResponse
                {
                    WarehouseId = w.WarehouseId,
                    WarehouseName = w.WarehouseName,
                    Location = w.Location,
                    ManagerId = w.ManagerId,
                    ManagerName = w.Manager?.LastName, // Tránh null
                    CreatedDate = (DateTime)w.CreatedDate,
                    ModifiedDate = w.ModifiedDate,
                    CreatedBy = w.CreatedBy,
                    ModifiedBy = w.ModifiedBy,
                    IsDeleted = w.IsDeleted,
                    InventoryRecords = w.InventoryRecords.Select(i => new InventoryRecordDto
                    {
                        InventoryId = i.InventoryId,
                        MaterialId = i.MaterialId,
                        QuantityOnHand = i.QuantityOnHand,
                        ReservedQuantity = i.ReservedQuantity,
                        ReorderLevel = i.ReorderLevel,
                        UpdatedAt = i.UpdatedAt
                    }).ToList()
                }).ToList();

                return response.SetOk(result);
            }
            catch (Exception ex)
            {
                return response.SetBadRequest("Lỗi: " + ex.Message);
            }
        }

        public async Task<ApiResponse> GetWarehouseInventoryAsync(int warehouseId)
        {
            var response = new ApiResponse();
            try
            {
                // 🚀 CẬP NHẬT 1: .Include thêm bảng Warehouse để lấy được tên kho (WarehouseName)
                var inventoryRecords = await _uow.Inventories.GetAllAsync(
                    filter: x => x.WarehouseId == warehouseId,
                    include: query => query.Include(i => i.Material).Include(i => i.Warehouse)
                );

                // 🚀 CẬP NHẬT 2: Map chuẩn xác sang danh sách DTO InventoryReportResponse của bạn
                var result = inventoryRecords.Select(i => new InventoryReportResponse
                {
                    MaterialId = i.MaterialId,
                    MaterialName = i.Material.MaterialName,
                    WarehouseName = i.Warehouse.WarehouseName, // Lấy từ bảng Warehouse đã Include ở trên
                    Unit = i.Material.Unit,
                    QuantityOnHand = i.QuantityOnHand,
                    ReservedQuantity = i.ReservedQuantity,
                    AvailableQuantity = i.QuantityOnHand - i.ReservedQuantity,
                    ReorderLevel = i.ReorderLevel,
                    IsLowStock = (i.QuantityOnHand - i.ReservedQuantity) <= i.ReorderLevel,
                    UpdatedAt = i.UpdatedAt
                }).ToList();

                return response.SetOk(result);
            }
            catch (Exception ex)
            {
                return response.SetBadRequest("Lỗi lấy dữ liệu vật tư trong kho: " + ex.Message);
            }
        }
    }
}