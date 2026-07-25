using cpms_Application.Request.WarehouseTransfer;
using cpms_Application.Response;

namespace cpms_Application.Interfaces
{
    public interface IWarehouseTransferService
    {
        Task<ApiResponse> CreateAsync(CreateWarehouseTransferRequest request);
        Task<ApiResponse> GetAllAsync();
        Task<ApiResponse> GetByIdAsync(int transferId);
        Task<ApiResponse> ApproveAsync(int transferId);
        Task<ApiResponse> RejectAsync(int transferId);
        Task<ApiResponse> ShipAsync(int transferId);
        Task<ApiResponse> ReceiveAsync(int transferId, ReceiveWarehouseTransferRequest? request);
        Task<ApiResponse> CancelAsync(int transferId);
    }
}
