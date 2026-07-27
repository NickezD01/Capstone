using System;

namespace cpms_Domain.Models
{
    public class ChatParticipant : Base
    {
        public int ParticipantId { get; set; }
        public int ConversationId { get; set; }
        public int UserId { get; set; }
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastReadAt { get; set; }
        public bool IsMuted { get; set; }

        public virtual ChatConversation Conversation { get; set; } = null!;
        public virtual UserAccount User { get; set; } = null!;
    }
}
