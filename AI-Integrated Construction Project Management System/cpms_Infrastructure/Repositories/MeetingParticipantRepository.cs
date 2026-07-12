using cpms_Application.Repository;
using cpms_Domain.Models;

namespace cpms_Infrastructure.Repositories
{
    public class MeetingParticipantRepository : GenericRepository<MeetingParticipant>, IMeetingParticipantRepository
    {
        public MeetingParticipantRepository(AppDbContext context) : base(context)
        {
        }
    }
}
