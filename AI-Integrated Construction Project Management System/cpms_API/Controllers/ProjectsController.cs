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
        [Authorize(Roles = "PM")]
        public async Task<IActionResult> CreateProject([FromBody] CreateProjectRequest request)
        {
            var response = await _projectService.CreateProjectAsync(request);
            return StatusCode((int)response.StatusCode, response);
        }

        // GET: api/projects
        [HttpGet]
        [Authorize(Roles = "ADMIN,PM,WAREHOUSE_MANAGER")]
        public async Task<IActionResult> GetAllProjects()
        {
            var response = await _projectService.GetAllProjectsAsync();
            return StatusCode((int)response.StatusCode, response);
        }

        // GET: api/projects/{id}
        [HttpGet("{id}")]
        [Authorize(Roles = "ADMIN,PM,WAREHOUSE_MANAGER")]
        public async Task<IActionResult> GetProjectById(int id)
        {
            var response = await _projectService.GetProjectByIdAsync(id);
            return StatusCode((int)response.StatusCode, response);
        }

        [HttpPost("import-word")]
        [Authorize(Roles = "PM")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> ImportProjectFromWord(IFormFile file)
        {
            var response = await _projectService.ImportProjectFromWordAsync(file);
            return StatusCode((int)response.StatusCode, response);
        }

        // POST: api/projects/tasks/{taskId}/materials
        [HttpPost("tasks/{taskId}/materials")]
        [Authorize(Roles = "PM")]
        public async Task<IActionResult> AssignMaterialRequirementToTask(int taskId, [FromBody] CreateTaskMaterialRequirementRequest request)
        {
            var response = await _projectService.AssignMaterialRequirementToTaskAsync(taskId, request);
            return StatusCode((int)response.StatusCode, response);
        }

        // GET: api/projects/{projectId}/material-requirements
        [HttpGet("{projectId}/material-requirements")]
        [Authorize(Roles = "ADMIN,PM,WAREHOUSE_MANAGER")]
        public async Task<IActionResult> GetMaterialRequirementsByProjectId(int projectId)
        {
            var response = await _projectService.GetMaterialRequirementsByProjectIdAsync(projectId);
            return StatusCode((int)response.StatusCode, response);
        }

        // GET: api/projects/{projectId}/calculate-mrp
        [HttpGet("{projectId}/calculate-mrp")]
        [Authorize(Roles = "ADMIN,PM,WAREHOUSE_MANAGER")]
        public async Task<IActionResult> CalculateMRPForProject(int projectId, [FromQuery] int? warehouseId)
        {
            var response = await _projectService.CalculateMRPForProjectAsync(projectId, warehouseId);
            return StatusCode((int)response.StatusCode, response);
        }
        [HttpPost("adjust-budget")]
        [Authorize(Roles = "ADMIN")]
        public async Task<IActionResult> AdjustProjectBudget([FromBody] AdjustBudgetRequest request)
        {
            var response = await _projectService.AdjustProjectBudgetAsync(request);
            return StatusCode((int)response.StatusCode, response);
        }

        // GET: api/projects/{projectId}/budget-histories
        [HttpGet("{projectId}/budget-histories")]
        [Authorize(Roles = "ADMIN,PM")]
        public async Task<IActionResult> GetBudgetHistories(int projectId)
        {
            var response = await _projectService.GetBudgetHistoriesByProjectIdAsync(projectId);
            return StatusCode((int)response.StatusCode, response);
        }
    }
}
