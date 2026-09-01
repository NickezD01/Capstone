using cpms_Application.Request.AiConstructionPlanner;
using cpms_Application.Response;

namespace cpms_Application.Interfaces
{
    public interface IAiConstructionPlannerService
    {
        Task<ApiResponse> GetQuestionsAsync();
        Task<ApiResponse> GeneratePlanJsonAsync(GenerateConstructionPlanRequest request);
        Task<ApiResponse> GenerateExcelAsync(GenerateConstructionPlanExcelRequest request);
    }
}
