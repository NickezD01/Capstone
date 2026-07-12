using cpms_Application.Interfaces;
using cpms_Application.Request.MaterialRequest;
using cpms_Application.Request.Project;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace cpms_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // Bảo mật các endpoint này
    public class ProjectsController : ControllerBase
    {
        private readonly IProjectService _projectService;

        public ProjectsController(IProjectService projectService)
        {
            _projectService = projectService;
        }

        // POST: api/projects
        [HttpPost]
        public async Task<IActionResult> CreateProject([FromBody] CreateProjectRequest request)
        {
            var response = await _projectService.CreateProjectAsync(request);
            if (!response.IsSuccess)
            {
                return BadRequest(response);
            }
            return Ok(response);
        }

        // GET: api/projects
        [HttpGet]
        public async Task<IActionResult> GetAllProjects()
        {
            var response = await _projectService.GetAllProjectsAsync();
            if (!response.IsSuccess)
            {
                return NotFound(response);
            }
            return Ok(response);
        }

        // GET: api/projects/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetProjectById(int id)
        {
            var response = await _projectService.GetProjectByIdAsync(id);
            if (!response.IsSuccess)
            {
                return NotFound(response);
            }
            return Ok(response);
        }

        // 🚀 CẬP NHẬT Ở ĐÂY: Xóa bỏ hoàn toàn [FromQuery] int pmUserId
        [HttpPost("import-word")]
        [Consumes("multipart/form-data")] // Ép kiểu Swagger/Frontend hiển thị nút chọn File
        public async Task<IActionResult> ImportProjectFromWord(IFormFile file)
        {
            // Truyền duy nhất file vào Service, ID sẽ được tự bóc tách từ Token trong ngầm định
            var response = await _projectService.ImportProjectFromWordAsync(file);
            if (!response.IsSuccess)
            {
                return BadRequest(response);
            }
            return Ok(response);
        }
        [HttpPost("tasks/{taskId}/materials")]
        public async Task<IActionResult> AssignMaterialRequirementToTask(int taskId, [FromBody] CreateTaskMaterialRequirementRequest request)
        {
            var response = await _projectService.AssignMaterialRequirementToTaskAsync(taskId, request);
            if (!response.IsSuccess)
            {
                return BadRequest(response);
            }
            return Ok(response);
        }
        [HttpGet("{projectId}/material-requirements")]
        public async Task<IActionResult> GetMaterialRequirementsByProjectId(int projectId)
        {
            var response = await _projectService.GetMaterialRequirementsByProjectIdAsync(projectId);
            if (!response.IsSuccess)
            {
                return NotFound(response);
            }
            return Ok(response);
        }
        [HttpGet("{projectId}/calculate-mrp")]
        public async Task<IActionResult> CalculateMRPForProject(int projectId)
        {
            var response = await _projectService.CalculateMRPForProjectAsync(projectId);
            if (!response.IsSuccess)
            {
                return BadRequest(response);
            }
            return Ok(response);
        }
    }
}