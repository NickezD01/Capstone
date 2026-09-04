using cpms_Application.Interfaces;
using cpms_Application.Request.ProjectPhase;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace cpms_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ProjectPhasesController : ControllerBase
    {
        private readonly IProjectPhaseService _service;

        public ProjectPhasesController(IProjectPhaseService service)
        {
            _service = service;
        }

        [HttpPost]
        [Authorize(Roles = "PM")]
        public async Task<IActionResult> Create([FromBody] CreateProjectPhaseRequest request)
        {
            var response = await _service.CreateAsync(request);
            return StatusCode((int)response.StatusCode, response);
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "ADMIN,PM,WAREHOUSE_MANAGER")]
        public async Task<IActionResult> Get(int id)
        {
            var response = await _service.GetByIdAsync(id);
            return StatusCode((int)response.StatusCode, response);
        }

        [HttpGet("project/{projectId}")]
        [Authorize(Roles = "ADMIN,PM,WAREHOUSE_MANAGER")]
        public async Task<IActionResult> GetByProjectId(int projectId)
        {
            var response = await _service.GetByProjectIdAsync(projectId);
            return StatusCode((int)response.StatusCode, response);
        }

        [HttpPut]
        [Authorize(Roles = "PM")]
        public async Task<IActionResult> Update([FromBody] UpdateProjectPhaseRequest request)
        {
            var response = await _service.UpdateAsync(request);
            return StatusCode((int)response.StatusCode, response);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "PM")]
        public async Task<IActionResult> Delete(int id)
        {
            var response = await _service.DeleteAsync(id);
            return StatusCode((int)response.StatusCode, response);
        }
    }
}
