namespace cpms_Application.Response.Chat
{
    public class MessageResponse
    {
        public int MessageId { get; set; }
        public int ConversationId { get; set; }
        public int SenderId { get; set; }
        public string? SenderName { get; set; }
        public string Body { get; set; } = null!;
        public string? AttachmentUrl { get; set; }
        public DateTime SentAt { get; set; }
        public DateTime? EditedAt { get; set; }
        public DateTime? DeletedAt { get; set; }
    }
}
