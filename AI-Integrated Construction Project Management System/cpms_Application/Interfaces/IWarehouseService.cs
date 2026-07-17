using cpms_Application.Request.Warehouse;
using cpms_Application.Response;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Application.Interfaces
{
    public interface IWarehouseService
    {
        Task<ApiResponse> CreateWarehouseAsync(CreateWarehouseRequest request);
        Task<ApiResponse> GetAllWarehousesAsync();

        Task<ApiResponse> GetWarehouseInventoryAsync(int warehouseId);
        Task<ApiResponse> GetInventoryAsync(int warehouseId, int variantId);
        Task<ApiResponse> AdjustInventoryAsync(InventoryAdjustmentRequest request);
        Task<ApiResponse> ReturnInventoryAsync(InventoryReturnRequest request);
        Task<ApiResponse> GetTransactionsAsync(int? warehouseId, int? variantId);
    }
}
