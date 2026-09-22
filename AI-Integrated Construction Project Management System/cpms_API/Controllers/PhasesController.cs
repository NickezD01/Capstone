using cpms_Application.Interfaces;
using cpms_Application.Request.Phase;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace cpms_API.Controllers;

[ApiController]
[Authorize]
public sealed class PhasesController : ControllerBase
{
    private readonly IPhaseService _phaseService;

    public PhasesController(IPhaseService phaseService)
    {
        _phaseService = phaseService;
    }

    [HttpPost("api/Projects/{projectId:int}/phases")]
    [Authorize(Roles = "PM")]
    public async Task<IActionResult> CreatePhase(int projectId, [FromBody] CreatePhaseRequest request)
    {
        var response = await _phaseService.CreatePhaseAsync(projectId, request);
        return StatusCode((int)response.StatusCode, response);
    }

        [HttpGet("api/Projects/{projectId:int}/phases")]
        [Authorize(Roles = "ADMIN,PM,WAREHOUSE_MANAGER,CUSTOMER")]
        public async Task<IActionResult> GetProjectPhases(int projectId)
    {
        var response = await _phaseService.GetPhasesByProjectAsync(projectId);
        return StatusCode((int)response.StatusCode, response);
    }

    [HttpGet("api/Phases/{phaseId:int}")]
    [Authorize(Roles = "ADMIN,PM,WAREHOUSE_MANAGER")]
    public async Task<IActionResult> GetPhase(int phaseId)
    {
        var response = await _phaseService.GetPhaseByIdAsync(phaseId);
        return StatusCode((int)response.StatusCode, response);
    }

    [HttpPut("api/Phases/{phaseId:int}")]
    [Authorize(Roles = "PM")]
    public async Task<IActionResult> UpdatePhase(int phaseId, [FromBody] UpdatePhaseRequest request)
    {
        var response = await _phaseService.UpdatePhaseAsync(phaseId, request);
        return StatusCode((int)response.StatusCode, response);
    }

    [HttpPost("api/Phases/{phaseId:int}/cancel")]
    [Authorize(Roles = "PM")]
    public async Task<IActionResult> CancelPhase(int phaseId, [FromBody] PhaseLifecycleRequest request)
    {
        var response = await _phaseService.CancelPhaseAsync(phaseId, request);
        return StatusCode((int)response.StatusCode, response);
    }
}
