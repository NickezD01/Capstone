using cpms_Application.Request.Tasks;
using cpms_Application.Response;

namespace cpms_Application.Interfaces
{
    public interface ITaskIssueService
    {
        Task<ApiResponse> CreateIssueAsync(int taskId, CreateTaskIssueRequest request);
        Task<ApiResponse> GetIssuesByTaskAsync(int taskId);
        Task<ApiResponse> ResolveIssueAsync(int issueId, ResolveTaskIssueRequest request);
    }
}
