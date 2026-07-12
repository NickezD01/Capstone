using cpms_Application.Request.PurchaseOrder;
using cpms_Application.Response;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Application.Interfaces
{
    public interface IPurchaseOrderService
    {
        Task<ApiResponse> CreatePurchaseOrderAsync(CreatePurchaseOrderRequest request);
        Task<ApiResponse> GetAllPurchaseOrdersAsync(); // Lấy danh sách
        Task<ApiResponse> ApprovePurchaseOrderAsync(int poId); // Phê duyệt
        Task<ApiResponse> RejectPurchaseOrderAsync(int poId);
        Task<ApiResponse> ImportToWarehouseAsync(int poId, int warehouseId);
    }
}
