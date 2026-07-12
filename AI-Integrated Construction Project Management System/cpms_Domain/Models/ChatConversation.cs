using System;
using System.Collections.Generic;

namespace cpms_Domain.Models
{
    public class ChatConversation : Base
    {
        public int ConversationId { get; set; }
        public int ProjectId { get; set; }
        public int? TaskId { get; set; }
        public string Title { get; set; } = null!;
        public ChatConversationType Type { get; set; } = ChatConversationType.PROJECT;
        public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;

        public virtual Project Project { get; set; } = null!;
        public virtual TaskItem? Task { get; set; }
        public virtual ICollection<ChatParticipant> Participants { get; set; } = new List<ChatParticipant>();
        public virtual ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
    }

    public enum ChatConversationType
    {
        PROJECT,
        TASK,
        MATERIAL_REQUEST,
        PURCHASE_ORDER
    }
}
