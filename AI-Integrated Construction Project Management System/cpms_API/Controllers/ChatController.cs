using cpms_Application.Interfaces;
using cpms_Application.Request.Chat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace cpms_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ChatController : ControllerBase
    {
        private readonly IChatService _chatService;

        public ChatController(IChatService chatService)
        {
            _chatService = chatService;
        }

        [HttpPost("conversations")]
        public async Task<IActionResult> CreateConversation([FromBody] CreateConversationRequest request)
        {
            var response = await _chatService.CreateConversationAsync(request);
            return response.IsSuccess ? Ok(response) : BadRequest(response);
        }

        [HttpGet("projects/{projectId}/conversations")]
        public async Task<IActionResult> GetProjectConversations(int projectId)
        {
            var response = await _chatService.GetProjectConversationsAsync(projectId);
            return response.IsSuccess ? Ok(response) : BadRequest(response);
        }

        [HttpGet("conversations/{conversationId}/messages")]
        public async Task<IActionResult> GetMessages(int conversationId)
        {
            var response = await _chatService.GetMessagesAsync(conversationId);
            return response.IsSuccess ? Ok(response) : BadRequest(response);
        }

        [HttpPost("conversations/{conversationId}/messages")]
        public async Task<IActionResult> SendMessage(int conversationId, [FromBody] SendMessageRequest request)
        {
            var response = await _chatService.SendMessageAsync(conversationId, request);
            return response.IsSuccess ? Ok(response) : BadRequest(response);
        }

        [HttpPut("messages/{messageId}")]
        public async Task<IActionResult> UpdateMessage(int messageId, [FromBody] UpdateMessageRequest request)
        {
            var response = await _chatService.UpdateMessageAsync(messageId, request);
            return response.IsSuccess ? Ok(response) : BadRequest(response);
        }

        [HttpDelete("messages/{messageId}")]
        public async Task<IActionResult> DeleteMessage(int messageId)
        {
            var response = await _chatService.DeleteMessageAsync(messageId);
            return response.IsSuccess ? Ok(response) : BadRequest(response);
        }

        [HttpPut("conversations/{conversationId}/read")]
        public async Task<IActionResult> MarkRead(int conversationId)
        {
            var response = await _chatService.MarkConversationReadAsync(conversationId);
            return response.IsSuccess ? Ok(response) : BadRequest(response);
        }
    }
}
