using cpms_Application.Interfaces;
using cpms_Application.Request.MaterialRequest;
using cpms_Application.Request.Project;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace cpms_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
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
            return Ok(response); // Hiện tại sẽ trả về JSON Object đầy đủ của dự án vừa tạo
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

        [HttpPost("import-word")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> ImportProjectFromWord(IFormFile file)
        {
            var response = await _projectService.ImportProjectFromWordAsync(file);
            if (!response.IsSuccess)
            {
                return BadRequest(response);
            }
            return Ok(response);
        }

        // POST: api/projects/tasks/{taskId}/materials
        [HttpPost("tasks/{taskId}/materials")]
        public async Task<IActionResult> AssignMaterialRequirementToTask(int taskId, [FromBody] CreateTaskMaterialRequirementRequest request)
        {
            var response = await _projectService.AssignMaterialRequirementToTaskAsync(taskId, request);
            if (!response.IsSuccess)
            {
                return BadRequest(response);
            }
            return Ok(response); // Hiện tại sẽ trả về dữ liệu định mức chi tiết thay vì chuỗi message thành công
        }

        // GET: api/projects/{projectId}/material-requirements
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

        // GET: api/projects/{projectId}/calculate-mrp
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
        [HttpPost("adjust-budget")]
        public async Task<IActionResult> AdjustProjectBudget([FromBody] AdjustBudgetRequest request)
        {
            var response = await _projectService.AdjustProjectBudgetAsync(request);
            if (!response.IsSuccess)
            {
                return BadRequest(response);
            }
            return Ok(response);
        }

        // GET: api/projects/{projectId}/budget-histories
        [HttpGet("{projectId}/budget-histories")]
        public async Task<IActionResult> GetBudgetHistories(int projectId)
        {
            var response = await _projectService.GetBudgetHistoriesByProjectIdAsync(projectId);
            if (!response.IsSuccess)
            {
                return NotFound(response);
            }
            return Ok(response);
        }
    }
}