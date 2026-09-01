using cpms_Application.Interfaces;
using cpms_Application.Request.AiConstructionPlanner;
using cpms_Application.Response.AiConstructionPlanner;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace cpms_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class AiConstructionPlannerController : ControllerBase
    {
        private readonly IAiConstructionPlannerService _plannerService;

        public AiConstructionPlannerController(IAiConstructionPlannerService plannerService)
        {
            _plannerService = plannerService;
        }

        [HttpGet("questions")]
        public async Task<IActionResult> GetQuestions()
        {
            var response = await _plannerService.GetQuestionsAsync();
            return StatusCode((int)response.StatusCode, response);
        }

        [HttpPost("generate-json")]
        public async Task<IActionResult> GenerateJson([FromBody] GenerateConstructionPlanRequest request)
        {
            var response = await _plannerService.GeneratePlanJsonAsync(request);
            return StatusCode((int)response.StatusCode, response);
        }

        [HttpPost("generate-excel")]
        public async Task<IActionResult> GenerateExcel([FromBody] GenerateConstructionPlanExcelRequest request)
        {
            var response = await _plannerService.GenerateExcelAsync(request);
            if (!response.IsSuccess)
                return StatusCode((int)response.StatusCode, response);

            var file = (ConstructionPlanExcelFileResponse)response.Result!;
            return File(file.Content, file.ContentType, file.FileName);
        }
    }
}
