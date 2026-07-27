using cpms_Domain.Models;

namespace cpms_Application.Request.Meeting
{
    public class CreateMeetingRequest
    {
        public int ProjectId { get; set; }
        public int? TaskId { get; set; }
        public string Subject { get; set; } = null!;
        public string? Agenda { get; set; }
        public DateTime StartDateTime { get; set; }
        public DateTime EndDateTime { get; set; }
        public string TimeZone { get; set; } = "UTC";
        public bool ScheduleWithTeams { get; set; } = true;
        public List<MeetingParticipantRequest> Participants { get; set; } = new List<MeetingParticipantRequest>();
    }

    public class MeetingParticipantRequest
    {
        public int? UserId { get; set; }
        public string Email { get; set; } = null!;
        public string? DisplayName { get; set; }
        public MeetingParticipantRole Role { get; set; } = MeetingParticipantRole.REQUIRED;
    }
}
