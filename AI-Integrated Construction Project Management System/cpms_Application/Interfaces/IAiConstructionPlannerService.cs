using cpms_Application.Request.AiConstructionPlanner;
using cpms_Application.Response;
using Microsoft.AspNetCore.Http;

namespace cpms_Application.Interfaces
{
    public interface IAiConstructionPlannerService
    {
        Task<ApiResponse> GetQuestionsAsync();
        Task<ApiResponse> GeneratePlanJsonAsync(GenerateConstructionPlanRequest request);
        Task<ApiResponse> GenerateExcelAsync(GenerateConstructionPlanExcelRequest request);
        Task<ApiResponse> GenerateProjectPhasesPreviewAsync(int projectId, GenerateProjectAiPhasesRequest request);
        Task<ApiResponse> GenerateProjectTasksPreviewAsync(int projectId, GenerateProjectAiTasksRequest request);
        Task<ApiResponse> ConfirmProjectAiPlanAsync(int projectId, ConfirmProjectAiPlanRequest request);
        Task<ApiResponse> CompleteProjectAiPlanAsync(int projectId, CompleteProjectAiPlanRequest request);
        Task<ApiResponse> ImportProjectFromWordAiAsync(IFormFile? file);
    }
}
