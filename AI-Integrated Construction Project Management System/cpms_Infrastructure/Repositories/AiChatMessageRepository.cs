using cpms_Application.Repository;
using cpms_Domain.Models;

namespace cpms_Infrastructure.Repositories
{
    public class AiChatMessageRepository : GenericRepository<AiChatMessage>, IAiChatMessageRepository
    {
        public AiChatMessageRepository(AppDbContext context) : base(context)
        {
        }
    }
}
