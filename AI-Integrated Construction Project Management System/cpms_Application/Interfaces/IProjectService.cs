using cpms_Application.Request.Project;
using cpms_Application.Response;
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
        Task<ApiResponse> UpdateProjectStatusAsync(int id, UpdateProjectStatusRequest request);
    }
}
