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
        Task<ApiResponse> ApproveRequestAsync(int requestId, ApproveMaterialRequest decision);
        Task<ApiResponse> RejectRequestAsync(int requestId);
        Task<ApiResponse> RejectRequestAsync(int requestId, RejectMaterialRequest decision);
        Task<ApiResponse> IssueRequestAsync(int requestId);
        Task<ApiResponse> ReleaseRequestAsync(int requestId);
        Task<ApiResponse> UpdatePendingRequestAsync(int requestId, UpdatePendingMaterialRequest request);
        Task<ApiResponse> CancelPendingRequestAsync(int requestId, CancelMaterialRequest request);


        Task<ApiResponse> GetRequestByIdAsync(int requestId);
        Task<ApiResponse> GetAllRequestsAsync();
        Task<ApiResponse> GetRequestsByProjectAsync(int projectId);
    }
}
