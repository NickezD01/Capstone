using cpms_Application.Repository;
using cpms_Domain.Models;

namespace cpms_Infrastructure.Repositories
{
    public class AiChatSessionRepository : GenericRepository<AiChatSession>, IAiChatSessionRepository
    {
        public AiChatSessionRepository(AppDbContext context) : base(context)
        {
        }
    }
}
