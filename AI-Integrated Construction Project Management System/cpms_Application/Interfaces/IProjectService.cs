using cpms_Application.Request.MaterialRequest;
using cpms_Application.Request.Project;
using cpms_Application.Response;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Application.Interfaces
{
    public interface IProjectService
    {
        Task<ApiResponse> CreateProjectAsync(CreateProjectRequest request);
        Task<ApiResponse> GetAllProjectsAsync();
        Task<ApiResponse> GetProjectByIdAsync(int id);
        Task<ApiResponse> AdjustProjectBudgetAsync(AdjustBudgetRequest request);
        Task<ApiResponse> GetBudgetHistoriesByProjectIdAsync(int projectId);

        Task<ApiResponse> ImportProjectFromWordAsync(IFormFile file);

        Task<ApiResponse> AssignMaterialRequirementToTaskAsync(int taskId, CreateTaskMaterialRequirementRequest request);

        Task<ApiResponse> GetMaterialRequirementsByProjectIdAsync(int projectId);
        Task<ApiResponse> CalculateMRPForProjectAsync(int projectId, int? warehouseId = null);
    }
}
