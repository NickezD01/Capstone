using cpms_Application.Repository;
using cpms_Domain.Models;

namespace cpms_Infrastructure.Repositories
{
    public class MeetingRepository : GenericRepository<Meeting>, IMeetingRepository
    {
        public MeetingRepository(AppDbContext context) : base(context)
        {
        }
    }
}
