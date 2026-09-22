using cpms_Application.Interfaces;
using cpms_Application.Request.AiConstructionPlanner;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace cpms_API.Controllers
{
    /// <summary>
    /// Step 12 AI preview/confirm workflow. Only the owning project manager may use
    /// these endpoints; the service enforces project ownership. Generation returns a
    /// stateless preview with temporary IDs and persists nothing; confirm persists
    /// the PM-edited proposal in a single transaction.
    /// </summary>
    [Route("api/Projects/{projectId:int}/ai")]
    [ApiController]
    [Authorize]
    public class ProjectAiController : ControllerBase
    {
        private readonly IAiConstructionPlannerService _plannerService;

        public ProjectAiController(IAiConstructionPlannerService plannerService)
        {
            _plannerService = plannerService;
        }

        [HttpPost("phases:generate")]
        [Authorize(Roles = "PM")]
        public async Task<IActionResult> GeneratePhases(int projectId, [FromBody] GenerateProjectAiPhasesRequest request)
        {
            var response = await _plannerService.GenerateProjectPhasesPreviewAsync(projectId, request);
            return StatusCode((int)response.StatusCode, response);
        }

        [HttpPost("tasks:generate")]
        [Authorize(Roles = "PM")]
        public async Task<IActionResult> GenerateTasks(int projectId, [FromBody] GenerateProjectAiTasksRequest request)
        {
            var response = await _plannerService.GenerateProjectTasksPreviewAsync(projectId, request);
            return StatusCode((int)response.StatusCode, response);
        }

        [HttpPost("confirm")]
        [Authorize(Roles = "PM")]
        public async Task<IActionResult> Confirm(int projectId, [FromBody] ConfirmProjectAiPlanRequest request)
        {
            var response = await _plannerService.ConfirmProjectAiPlanAsync(projectId, request);
            return StatusCode((int)response.StatusCode, response);
        }

        [HttpPost("complete")]
        [Authorize(Roles = "PM")]
        public async Task<IActionResult> Complete(int projectId, [FromBody] CompleteProjectAiPlanRequest request)
        {
            var response = await _plannerService.CompleteProjectAiPlanAsync(projectId, request);
            return StatusCode((int)response.StatusCode, response);
        }
    }
}
