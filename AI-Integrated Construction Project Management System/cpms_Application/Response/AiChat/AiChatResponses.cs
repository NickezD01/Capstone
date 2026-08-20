using cpms_Domain.Models;

namespace cpms_Application.Response.AiChat
{
    public class AiChatSessionResponse
    {
        public int SessionId { get; set; }
        public int UserId { get; set; }
        public int? ProjectId { get; set; }
        public string Title { get; set; } = null!;
        public DateTime? CreatedAt { get; set; }
        public DateTime? LastMessageAt { get; set; }
        public int MessageCount { get; set; }
    }

    public class AiChatMessageResponse
    {
        public int MessageId { get; set; }
        public int SessionId { get; set; }
        public AiChatRole Role { get; set; }
        public string Content { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime SentAt { get; set; }
    }

    public class AiChatReplyResponse
    {
        public AiChatMessageResponse UserMessage { get; set; } = null!;
        public AiChatMessageResponse AssistantMessage { get; set; } = null!;
        public bool UsedWebSearch { get; set; }
        public List<AiChatWebSearchSource> WebSearchSources { get; set; } = new();
    }

    public class AiChatWebSearchSource
    {
        public string Title { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
    }
}
