using cpms_Application.Request.PurchaseOrder;
using cpms_Application.Response;
using cpms_Application.Request.Warehouse;
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
        Task<ApiResponse> GetProcurementShortagesAsync();
        Task<ApiResponse> GetAllPurchaseOrdersAsync(); // Lấy danh sách
        Task<ApiResponse> GetPurchaseOrderByIdAsync(int poId);
        Task<ApiResponse> ApprovePurchaseOrderAsync(int poId, PurchaseOrderActionRequest? request = null); // Phê duyệt
        Task<ApiResponse> RejectPurchaseOrderAsync(int poId, PurchaseOrderActionRequest? request = null);
        Task<ApiResponse> ImportToWarehouseAsync(int poId, int warehouseId);
        Task<ApiResponse> ReceivePurchaseOrderAsync(int poId, ReceivePurchaseOrderRequest request);
        Task<ApiResponse> CancelPurchaseOrderAsync(int poId, PurchaseOrderActionRequest? request = null);
        Task<ApiResponse> MarkProcessingAsync(int poId, PurchaseOrderActionRequest? request = null);
        Task<ApiResponse> MarkShippedAsync(int poId, PurchaseOrderActionRequest? request = null);
    }
}
