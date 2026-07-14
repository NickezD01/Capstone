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
            if (!response.IsSuccess)
            {
                return BadRequest(response);
            }
            return Ok(response);
        }

        // GET: api/task/project/{projectId}
        [HttpGet("project/{projectId}")]
        public async Task<IActionResult> GetTasksByProject(int projectId)
        {
            var response = await _taskService.GetTasksByProjectAsync(projectId);
            if (!response.IsSuccess)
            {
                return NotFound(response);
            }
            return Ok(response);
        }

        [HttpGet("project/{projectId}/material-requirements")]
        public async Task<IActionResult> GetMaterialRequirements(int projectId)
        {
            // Lưu ý: Đảm bảo trong ITaskService đã khai báo hàm này 
            // Hoặc nếu bạn đặt nó bên IProjectService thì gọi qua _projectService nhé.
            var response = await _taskService.GetMaterialRequirementsByProjectIdAsync(projectId);
            if (!response.IsSuccess)
            {
                return NotFound(response);
            }
            return Ok(response);
        }
    }
}
