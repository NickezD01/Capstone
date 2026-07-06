using cpms_Application.Interfaces;
using cpms_Application.Request.ProgressReport;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace cpms_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // Bảo mật endpoint bằng JWT Token
    public class ProgressReportController : ControllerBase
    {
        private readonly IProgressReportService _progressReportService;

        public ProgressReportController(IProgressReportService progressReportService)
        {
            _progressReportService = progressReportService;
        }

        // POST: api/progressreport
        [HttpPost]
        public async Task<IActionResult> SubmitProgressReport([FromBody] SubmitProgressReportRequest request)
        {
            var response = await _progressReportService.SubmitReportAsync(request);
            if (!response.IsSuccess)
            {
                return BadRequest(response);
            }
            return Ok(response);
        }

        // GET: api/progressreport/task/{taskId}
        [HttpGet("task/{taskId}")]
        public async Task<IActionResult> GetReportsByTaskId(int taskId)
        {
            var response = await _progressReportService.GetReportsByTaskIdAsync(taskId);
            if (!response.IsSuccess)
            {
                return NotFound(response);
            }
            return Ok(response);
        }
    }
}