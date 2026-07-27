using cpms_Domain.Models;

namespace cpms_Application.Request.Chat
{
    public class CreateConversationRequest
    {
        public int ProjectId { get; set; }
        public int? TaskId { get; set; }
        public string Title { get; set; } = null!;
        public ChatConversationType Type { get; set; } = ChatConversationType.PROJECT;
        public List<int> ParticipantUserIds { get; set; } = new List<int>();
    }
}
