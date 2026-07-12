using cpms_Application.Request.MaterialRequest;
using cpms_Application.Response;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Application.Interfaces
{
    public interface IMaterialRequestService
    {
        Task<ApiResponse> CreateRequestAsync(CreateMaterialRequest request);
        Task<ApiResponse> CreateRequestByTaskIdAsync(int taskId);
        Task<ApiResponse> ApproveRequestAsync(int requestId);
        Task<ApiResponse> RejectRequestAsync(int requestId);


        Task<ApiResponse> GetRequestByIdAsync(int requestId);
        Task<ApiResponse> GetAllRequestsAsync();
        Task<ApiResponse> GetRequestsByProjectAsync(int projectId);
    }
}
