using cpms_Application.Interfaces;
using cpms_Application.Request.AiChat;
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

        [HttpPost("sessions")]
        public async Task<IActionResult> CreateSession([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CreateAiChatSessionRequest? request)
        {
            var response = await _aiChatService.CreateSessionAsync(request ?? new CreateAiChatSessionRequest());
            return StatusCode((int)response.StatusCode, response);
        }

        [HttpGet("sessions")]
        public async Task<IActionResult> GetSessions()
        {
            var response = await _aiChatService.GetSessionsAsync();
            return StatusCode((int)response.StatusCode, response);
        }

        [HttpGet("sessions/{sessionId:int}/messages")]
        public async Task<IActionResult> GetMessages(int sessionId)
        {
            var response = await _aiChatService.GetMessagesAsync(sessionId);
            return StatusCode((int)response.StatusCode, response);
        }

        [HttpPost("sessions/{sessionId:int}/messages")]
        public async Task<IActionResult> SendMessage(int sessionId, [FromBody] SendAiChatMessageRequest request)
        {
            var response = await _aiChatService.SendMessageAsync(sessionId, request);
            return StatusCode((int)response.StatusCode, response);
        }

        [HttpDelete("sessions/{sessionId:int}")]
        public async Task<IActionResult> DeleteSession(int sessionId)
        {
            var response = await _aiChatService.DeleteSessionAsync(sessionId);
            return StatusCode((int)response.StatusCode, response);
        }
    }
}
