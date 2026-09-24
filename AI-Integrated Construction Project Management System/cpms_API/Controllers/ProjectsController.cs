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
        private readonly IProjectExportService _projectExportService;
        private readonly IAiConstructionPlannerService _plannerService;
        private readonly IRiskAssessmentService _riskAssessmentService;

        public ProjectsController(
            IProjectService projectService,
            IProjectExportService projectExportService,
            IAiConstructionPlannerService plannerService,
            IRiskAssessmentService riskAssessmentService)
        {
            _projectService = projectService;
            _projectExportService = projectExportService;
            _plannerService = plannerService;
            _riskAssessmentService = riskAssessmentService;
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
        [Authorize(Roles = "ADMIN,PM,WAREHOUSE_MANAGER,CUSTOMER")]
        public async Task<IActionResult> GetAllProjects()
        {
            var response = await _projectService.GetAllProjectsAsync();
            return StatusCode((int)response.StatusCode, response);
        }

        // GET: api/projects/{id}
        [HttpGet("{id}")]
        [Authorize(Roles = "ADMIN,PM,WAREHOUSE_MANAGER,CUSTOMER")]
        public async Task<IActionResult> GetProjectById(int id)
        {
            var response = await _projectService.GetProjectByIdAsync(id);
            return StatusCode((int)response.StatusCode, response);
        }

        // GET: api/projects/{id}/context (minimal header for assigned site workers)
        [HttpGet("{id}/context")]
        [Authorize(Roles = "WORKER")]
        public async Task<IActionResult> GetProjectContext(int id)
        {
            var response = await _projectService.GetProjectContextAsync(id);
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

        [HttpPost("import-word-ai")]
        [Authorize(Roles = "PM")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> ImportProjectFromWordAi(IFormFile file)
        {
            var response = await _plannerService.ImportProjectFromWordAiAsync(file);
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

        // Creates and stores a versioned MRP planning snapshot.
        [HttpPost("{projectId}/mrp-runs")]
        [Authorize(Roles = "PM,WAREHOUSE_MANAGER")]
        public async Task<IActionResult> CalculateMRPForProject(int projectId, [FromQuery] int? warehouseId)
        {
            var response = await _projectService.CalculateMRPForProjectAsync(projectId, warehouseId);
            return StatusCode((int)response.StatusCode, response);
        }

        [HttpGet("{projectId}/mrp-runs/latest")]
        [Authorize(Roles = "ADMIN,PM,WAREHOUSE_MANAGER")]
        public async Task<IActionResult> GetLatestMRPForProject(int projectId, [FromQuery] int warehouseId)
        {
            var response = await _projectService.GetLatestMRPForProjectAsync(projectId, warehouseId);
            return StatusCode((int)response.StatusCode, response);
        }
        [HttpPost("adjust-budget")]
        [Authorize(Roles = "PM")]
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

        [HttpGet("{projectId:int}/risks")]
        [Authorize(Roles = "ADMIN,PM")]
        public async Task<IActionResult> GetProjectRisks(int projectId)
        {
            var response = await _riskAssessmentService.GetProjectRisksAsync(projectId);
            return StatusCode((int)response.StatusCode, response);
        }

        [HttpPost("{projectId:int}/risks/recommend-actions")]
        [Authorize(Roles = "PM")]
        public async Task<IActionResult> RecommendRiskActions(int projectId)
        {
            var response = await _riskAssessmentService.RecommendActionsAsync(projectId);
            return StatusCode((int)response.StatusCode, response);
        }

        [HttpPut("{projectId:int}")]
        [Authorize(Roles = "PM")]
        public async Task<IActionResult> UpdateProject(int projectId, UpdateProjectRequest request)
        {
            var response = await _projectService.UpdateProjectAsync(projectId, request);
            return StatusCode((int)response.StatusCode, response);
        }

        [HttpPost("{projectId:int}/start")]
        [Authorize(Roles = "PM")]
        public Task<IActionResult> Start(int projectId, ProjectLifecycleRequest request) => ChangeStatus(projectId, "start", request);

        [HttpPost("{projectId:int}/pause")]
        [Authorize(Roles = "PM")]
        public Task<IActionResult> Pause(int projectId, ProjectLifecycleRequest request) => ChangeStatus(projectId, "pause", request);

        [HttpPost("{projectId:int}/cancel")]
        [Authorize(Roles = "PM")]
        public Task<IActionResult> Cancel(int projectId, ProjectLifecycleRequest request) => ChangeStatus(projectId, "cancel", request);

        [HttpPost("{projectId:int}/reopen")]
        [Authorize(Roles = "PM")]
        public Task<IActionResult> Reopen(int projectId, ProjectLifecycleRequest request) => ChangeStatus(projectId, "reopen", request);

        [HttpPost("{projectId:int}/complete")]
        [Authorize(Roles = "PM")]
        public Task<IActionResult> Complete(int projectId, ProjectLifecycleRequest request) => ChangeStatus(projectId, "complete", request);

        private async Task<IActionResult> ChangeStatus(int projectId, string action, ProjectLifecycleRequest request)
        {
            var response = await _projectService.ChangeProjectStatusAsync(projectId, action, request);
            return StatusCode((int)response.StatusCode, response);
        }

        [HttpPut("{projectId:int}/project-manager")]
        [Authorize(Roles = "ADMIN")]
        public async Task<IActionResult> ReassignProjectManager(int projectId, ReassignProjectManagerRequest request)
        {
            var response = await _projectService.ReassignProjectManagerAsync(projectId, request);
            return StatusCode((int)response.StatusCode, response);
        }

        [HttpPut("{projectId:int}/customer")]
        [Authorize(Roles = "PM")]
        public async Task<IActionResult> AssignCustomer(int projectId, AssignCustomerRequest request)
        {
            var response = await _projectService.AssignCustomerAsync(projectId, request);
            return StatusCode((int)response.StatusCode, response);
        }

        [HttpGet("{projectId:int}/export")]
        [Authorize(Roles = "ADMIN,PM,WAREHOUSE_MANAGER,CUSTOMER")]
        public async Task<IActionResult> ExportProject(int projectId)
        {
            var response = await _projectExportService.ExportProjectAsync(projectId);
            if (!response.IsSuccess)
                return StatusCode((int)response.StatusCode, response);

            var file = (cpms_Application.Response.AiConstructionPlanner.ConstructionPlanExcelFileResponse)response.Result!;
            return File(file.Content, file.ContentType, file.FileName);
        }
    }
}
