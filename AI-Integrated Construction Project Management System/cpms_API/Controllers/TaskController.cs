using cpms_Application.Interfaces;
using cpms_Application.Request.Tasks;
using cpms_Application.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Threading.Tasks;

namespace cpms_API.Controllers
{
    [Route("api/Tasks")]
    [ApiController]
    [Authorize] // Bảo mật endpoint bằng JWT Token
    public class TaskController : ControllerBase
    {
        private readonly ITaskService _taskService;
        private readonly ITaskIssueService _taskIssueService;

        public TaskController(ITaskService taskService, ITaskIssueService taskIssueService)
        {
            _taskService = taskService;
            _taskIssueService = taskIssueService;
        }

        // POST: /api/Phases/{phaseId}/tasks
        [HttpPost("~/api/Phases/{phaseId:int}/tasks")]
        [Authorize(Roles = "PM")]
        public async Task<IActionResult> CreateTaskUnderPhase(int phaseId, [FromBody] CreateTaskRequest request)
        {
            var response = await _taskService.CreateTaskAsync(phaseId, request);
            return StatusCode((int)response.StatusCode, response);
        }

        // DEPRECATED POST: /api/task -> returns 410 Gone pointing to POST /api/Phases/{phaseId}/tasks
        [HttpPost("~/api/task")]
        [Authorize(Roles = "PM")]
        public IActionResult DeprecatedCreateTask()
        {
            var response = new ApiResponse().SetApiResponse(
                HttpStatusCode.Gone,
                false,
                "POST /api/task is deprecated. Use POST /api/Phases/{phaseId}/tasks instead.");
            return StatusCode(StatusCodes.Status410Gone, response);
        }

        // GET: /api/Projects/{projectId}/tasks
        [HttpGet("~/api/Projects/{projectId:int}/tasks")]
        [Authorize(Roles = "ADMIN,PM,WAREHOUSE_MANAGER,CUSTOMER")]
        public async Task<IActionResult> GetTasksByProject(int projectId)
        {
            var response = await _taskService.GetTasksByProjectAsync(projectId);
            return StatusCode((int)response.StatusCode, response);
        }

        // GET: /api/Tasks/{taskId}
        [HttpGet("{taskId:int}")]
        [Authorize(Roles = "ADMIN,PM,WAREHOUSE_MANAGER,WORKER")]
        public async Task<IActionResult> GetTaskById(int taskId)
        {
            var response = await _taskService.GetTaskByIdAsync(taskId);
            return StatusCode((int)response.StatusCode, response);
        }

        // GET: /api/Tasks/assigned
        [HttpGet("assigned")]
        [Authorize(Roles = "PM,WORKER")]
        public async Task<IActionResult> GetAssignedTasks()
        {
            var response = await _taskService.GetAssignedTasksAsync();
            return StatusCode((int)response.StatusCode, response);
        }

        // PUT: /api/Tasks/{taskId}
        [HttpPut("{taskId:int}")]
        [Authorize(Roles = "PM")]
        public async Task<IActionResult> UpdateTask(int taskId, UpdateTaskRequest request)
        {
            var response = await _taskService.UpdateTaskAsync(taskId, request);
            return StatusCode((int)response.StatusCode, response);
        }

        // POST: /api/Tasks/{taskId}/cancel
        [HttpPost("{taskId:int}/cancel")]
        [Authorize(Roles = "PM")]
        public Task<IActionResult> Cancel(int taskId, TaskLifecycleRequest request) => ChangeStatus(taskId, "cancel", request);

        // POST: /api/Tasks/{taskId}/reject
        [HttpPost("{taskId:int}/reject")]
        [Authorize(Roles = "PM")]
        public Task<IActionResult> Reject(int taskId, TaskLifecycleRequest request) => ChangeStatus(taskId, "reject", request);

        // POST: /api/Tasks/{taskId}/reopen
        [HttpPost("{taskId:int}/reopen")]
        [Authorize(Roles = "PM")]
        public Task<IActionResult> Reopen(int taskId, TaskLifecycleRequest request) => ChangeStatus(taskId, "reopen", request);

        private async Task<IActionResult> ChangeStatus(int taskId, string action, TaskLifecycleRequest request)
        {
            var response = await _taskService.ChangeTaskStatusAsync(taskId, action, request);
            return StatusCode((int)response.StatusCode, response);
        }

        [HttpPost("{taskId:int}/issues")]
        [Authorize(Roles = "PM,WORKER")]
        public async Task<IActionResult> ReportIssue(int taskId, [FromBody] CreateTaskIssueRequest request)
        {
            var response = await _taskIssueService.CreateIssueAsync(taskId, request);
            return StatusCode((int)response.StatusCode, response);
        }

        [HttpGet("{taskId:int}/issues")]
        [Authorize(Roles = "ADMIN,PM,WORKER")]
        public async Task<IActionResult> GetIssues(int taskId)
        {
            var response = await _taskIssueService.GetIssuesByTaskAsync(taskId);
            return StatusCode((int)response.StatusCode, response);
        }

        [HttpPut("issues/{issueId:int}/resolve")]
        [Authorize(Roles = "PM")]
        public async Task<IActionResult> ResolveIssue(int issueId, [FromBody] ResolveTaskIssueRequest request)
        {
            var response = await _taskIssueService.ResolveIssueAsync(issueId, request);
            return StatusCode((int)response.StatusCode, response);
        }
    }
}
