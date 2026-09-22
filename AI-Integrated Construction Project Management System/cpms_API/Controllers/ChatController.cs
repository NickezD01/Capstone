using cpms_Application.Interfaces;
using cpms_Application.Request.Chat;
using cpms_Application.Response;
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

        // Chat is retired: every endpoint returns 410 Gone.
        [HttpPost("conversations")]
        public IActionResult CreateConversation([FromBody] CreateConversationRequest request) => Gone();

        [HttpGet("projects/{projectId}/conversations")]
        public IActionResult GetProjectConversations(int projectId) => Gone();

        [HttpGet("conversations/{conversationId}/messages")]
        public IActionResult GetMessages(int conversationId) => Gone();

        [HttpPost("conversations/{conversationId}/messages")]
        public IActionResult SendMessage(int conversationId, [FromBody] SendMessageRequest request) => Gone();

        [HttpPut("messages/{messageId}")]
        public IActionResult UpdateMessage(int messageId, [FromBody] UpdateMessageRequest request) => Gone();

        [HttpDelete("messages/{messageId}")]
        public IActionResult DeleteMessage(int messageId) => Gone();

        [HttpPut("conversations/{conversationId}/read")]
        public IActionResult MarkRead(int conversationId) => Gone();

        private ObjectResult Gone() =>
            StatusCode((int)System.Net.HttpStatusCode.Gone,
                new ApiResponse().SetApiResponse(System.Net.HttpStatusCode.Gone, false,
                    "Chat is no longer supported."));
    }
}
