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
        Task<ApiResponse> GetTasksByProjectAsync(int projectId);
    }
}
