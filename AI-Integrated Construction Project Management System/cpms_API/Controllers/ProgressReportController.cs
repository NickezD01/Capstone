using cpms_Application.Interfaces;
using cpms_Application.Request.ProgressReport;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace cpms_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ProgressReportController : ControllerBase
    {
        private readonly IProgressReportService _progressReportService;

        public ProgressReportController(IProgressReportService progressReportService)
        {
            _progressReportService = progressReportService;
        }

        // POST: api/progressreport
        [HttpPost]
        [Authorize(Roles = "PM")]
        public async Task<IActionResult> SubmitProgressReport([FromBody] SubmitProgressReportRequest request)
        {
            var response = await _progressReportService.SubmitReportAsync(request);
            return StatusCode((int)response.StatusCode, response);
        }

        // GET: api/progressreport/task/{taskId}
        [HttpGet("task/{taskId}")]
        [Authorize(Roles = "ADMIN,PM")]
        public async Task<IActionResult> GetReportsByTaskId(int taskId)
        {
            var response = await _progressReportService.GetReportsByTaskIdAsync(taskId);
            return StatusCode((int)response.StatusCode, response);
        }
    }
}
