using cpms_Application.Request.Category;
using cpms_Application.Response;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Application.Interfaces
{
    public interface ICategoryService
    {
        Task<ApiResponse> CreateCategoryAsync(CreateCategoryRequest request);
        Task<ApiResponse> GetAllCategoriesAsync();
        Task<ApiResponse> GetCategoryByIdAsync(int id);
        Task<ApiResponse> UpdateCategoryAsync(int id, UpdateCategoryRequest request);
        Task<ApiResponse> DeleteCategoryAsync(int id);
    }
}
