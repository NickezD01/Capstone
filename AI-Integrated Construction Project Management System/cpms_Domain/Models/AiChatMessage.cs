using System;

namespace cpms_Domain.Models
{
    public class AiChatMessage : Base
    {
        public int MessageId { get; set; }
        public int SessionId { get; set; }
        public AiChatRole Role { get; set; }
        public string Content { get; set; } = null!;
        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        public virtual AiChatSession Session { get; set; } = null!;
    }

    public enum AiChatRole
    {
        User,
        Assistant
    }
}
