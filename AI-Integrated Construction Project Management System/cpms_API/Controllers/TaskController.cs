using cpms_Application.Interfaces;
using cpms_Application.Request.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace cpms_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // Bảo mật endpoint bằng JWT Token
    public class TaskController : ControllerBase
    {
        private readonly ITaskService _taskService;

        public TaskController(ITaskService taskService)
        {
            _taskService = taskService;
        }

        // POST: api/task
        [HttpPost]
        [Authorize(Roles = "PM")]
        public async Task<IActionResult> CreateTask([FromBody] CreateTaskRequest request)
        {
            var response = await _taskService.CreateTaskAsync(request);
            return StatusCode((int)response.StatusCode, response);
        }

        // GET: api/task/project/{projectId}
        [HttpGet("project/{projectId}")]
        [Authorize(Roles = "ADMIN,PM,WAREHOUSE_MANAGER")]
        public async Task<IActionResult> GetTasksByProject(int projectId)
        {
            var response = await _taskService.GetTasksByProjectAsync(projectId);
            return StatusCode((int)response.StatusCode, response);
        }

        [HttpGet("project/{projectId}/material-requirements")]
        [Authorize(Roles = "ADMIN,PM,WAREHOUSE_MANAGER")]
        public async Task<IActionResult> GetMaterialRequirements(int projectId)
        {
            // Lưu ý: Đảm bảo trong ITaskService đã khai báo hàm này 
            // Hoặc nếu bạn đặt nó bên IProjectService thì gọi qua _projectService nhé.
            var response = await _taskService.GetMaterialRequirementsByProjectIdAsync(projectId);
            return StatusCode((int)response.StatusCode, response);
        }

        [HttpGet("assigned")]
        [Authorize(Roles = "PM")]
        public async Task<IActionResult> GetAssignedTasks()
        {
            var response = await _taskService.GetAssignedTasksAsync();
            return StatusCode((int)response.StatusCode, response);
        }

        [HttpPut("{taskId:int}")]
        [Authorize(Roles = "PM")]
        public async Task<IActionResult> UpdateTask(int taskId, UpdateTaskRequest request)
        {
            var response = await _taskService.UpdateTaskAsync(taskId, request);
            return StatusCode((int)response.StatusCode, response);
        }

        [HttpPost("{taskId:int}/cancel")]
        [Authorize(Roles = "PM")]
        public Task<IActionResult> Cancel(int taskId, TaskLifecycleRequest request) => ChangeStatus(taskId, "cancel", request);

        [HttpPost("{taskId:int}/reject")]
        [Authorize(Roles = "PM")]
        public Task<IActionResult> Reject(int taskId, TaskLifecycleRequest request) => ChangeStatus(taskId, "reject", request);

        [HttpPost("{taskId:int}/reopen")]
        [Authorize(Roles = "PM")]
        public Task<IActionResult> Reopen(int taskId, TaskLifecycleRequest request) => ChangeStatus(taskId, "reopen", request);

        private async Task<IActionResult> ChangeStatus(int taskId, string action, TaskLifecycleRequest request)
        {
            var response = await _taskService.ChangeTaskStatusAsync(taskId, action, request);
            return StatusCode((int)response.StatusCode, response);
        }
    }
}
