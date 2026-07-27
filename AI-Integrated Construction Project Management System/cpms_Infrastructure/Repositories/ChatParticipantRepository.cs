using cpms_Application.Repository;
using cpms_Domain.Models;

namespace cpms_Infrastructure.Repositories
{
    public class ChatParticipantRepository : GenericRepository<ChatParticipant>, IChatParticipantRepository
    {
        public ChatParticipantRepository(AppDbContext context) : base(context)
        {
        }
    }
}
