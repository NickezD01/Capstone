using System;

namespace cpms_Domain.Models
{
    public class ChatMessage : Base
    {
        public int MessageId { get; set; }
        public int ConversationId { get; set; }
        public int SenderId { get; set; }
        public string Body { get; set; } = null!;
        public string? AttachmentUrl { get; set; }
        public DateTime SentAt { get; set; } = DateTime.UtcNow;
        public DateTime? EditedAt { get; set; }
        public DateTime? DeletedAt { get; set; }

        public virtual ChatConversation Conversation { get; set; } = null!;
        public virtual UserAccount Sender { get; set; } = null!;
    }
}
