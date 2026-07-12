using cpms_Domain.Models;

namespace cpms_Application.Response.Chat
{
    public class ConversationResponse
    {
        public int ConversationId { get; set; }
        public int ProjectId { get; set; }
        public int? TaskId { get; set; }
        public string Title { get; set; } = null!;
        public ChatConversationType Type { get; set; }
        public DateTime LastMessageAt { get; set; }
        public List<ConversationParticipantResponse> Participants { get; set; } = new List<ConversationParticipantResponse>();
    }

    public class ConversationParticipantResponse
    {
        public int UserId { get; set; }
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public DateTime JoinedAt { get; set; }
        public DateTime? LastReadAt { get; set; }
    }
}
