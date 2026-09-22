using cpms_Application.Interfaces;
using cpms_Application.Request.AiChat;
using cpms_Application.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace cpms_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class AiChatController : ControllerBase
    {
        private readonly IAiChatService _aiChatService;

        public AiChatController(IAiChatService aiChatService)
        {
            _aiChatService = aiChatService;
        }

        // AI chat is retired: every endpoint returns 410 Gone.
        [HttpPost("sessions")]
        public IActionResult CreateSession([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CreateAiChatSessionRequest? request) => Gone();

        [HttpGet("sessions")]
        public IActionResult GetSessions() => Gone();

        [HttpGet("sessions/{sessionId:int}/messages")]
        public IActionResult GetMessages(int sessionId) => Gone();

        [HttpPost("sessions/{sessionId:int}/messages")]
        public IActionResult SendMessage(int sessionId, [FromBody] SendAiChatMessageRequest request) => Gone();

        [HttpDelete("sessions/{sessionId:int}")]
        public IActionResult DeleteSession(int sessionId) => Gone();

        private ObjectResult Gone() =>
            StatusCode((int)System.Net.HttpStatusCode.Gone,
                new ApiResponse().SetApiResponse(System.Net.HttpStatusCode.Gone, false,
                    "AI chat is no longer supported."));
    }
}
