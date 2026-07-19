using cpms_Application.Request.Tasks;
using cpms_Application.Response;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Application.Interfaces
{
    public interface ITaskService
    {
        Task<ApiResponse> CreateTaskAsync(CreateTaskRequest request);
        Task<ApiResponse> GetTaskByIdAsync(int taskId);
        Task<ApiResponse> GetTasksByProjectAsync(int projectId);

        Task<ApiResponse> GetMaterialRequirementsByProjectIdAsync(int projectId);
        Task<ApiResponse> GetAssignedTasksAsync();
        Task<ApiResponse> UpdateTaskAsync(int taskId, UpdateTaskRequest request);
        Task<ApiResponse> ChangeTaskStatusAsync(int taskId, string action, TaskLifecycleRequest request);
    }
}
