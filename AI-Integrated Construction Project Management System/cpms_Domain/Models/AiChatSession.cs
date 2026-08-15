using System;
using System.Collections.Generic;

namespace cpms_Domain.Models
{
    public class AiChatSession : Base
    {
        public int SessionId { get; set; }
        public int UserId { get; set; }
        public int? ProjectId { get; set; }
        public string Title { get; set; } = "New chat";
        public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;

        public virtual UserAccount User { get; set; } = null!;
        public virtual Project? Project { get; set; }
        public virtual ICollection<AiChatMessage> Messages { get; set; } = new List<AiChatMessage>();
    }
}
