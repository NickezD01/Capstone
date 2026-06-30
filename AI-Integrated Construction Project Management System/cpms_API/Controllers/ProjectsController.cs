using cpms_Application.Authorization;
using cpms_Application.Interfaces;
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
        [Authorize(Roles = AppRoles.Admin)]
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
        [Authorize(Roles = AppRoles.Admin + "," + AppRoles.ProjectManager)]
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
        [Authorize(Roles = AppRoles.Admin + "," + AppRoles.ProjectManager)]
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

        // PUT: api/projects/{id}/status
        [Authorize(Roles = AppRoles.Admin + "," + AppRoles.ProjectManager)]
        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateProjectStatus(int id, [FromBody] UpdateProjectStatusRequest request)
        {
            var response = await _projectService.UpdateProjectStatusAsync(id, request);
            if (!response.IsSuccess)
            {
                return BadRequest(response);
            }
            return Ok(response);
        }
    }
}