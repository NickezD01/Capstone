using cpms_Application.Response;

namespace cpms_Application.Interfaces
{
    public interface IRiskAssessmentService
    {
        Task<ApiResponse> GetProjectRisksAsync(int projectId);
        Task<ApiResponse> RecommendActionsAsync(int projectId);
    }
}
