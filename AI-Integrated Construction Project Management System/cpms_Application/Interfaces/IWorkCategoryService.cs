using cpms_Application.Request.WorkCategory;
using cpms_Application.Response;

namespace cpms_Application.Interfaces
{
    public interface IWorkCategoryService
    {
        Task<ApiResponse> GetAllAsync();
        Task<ApiResponse> GetByIdAsync(int id);
        Task<ApiResponse> CreateAsync(CreateWorkCategoryRequest request);
        Task<ApiResponse> UpdateAsync(int id, UpdateWorkCategoryRequest request);
        Task<ApiResponse> DeleteAsync(int id);
    }
}
