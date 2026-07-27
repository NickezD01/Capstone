using cpms_Domain.Models;

namespace cpms_Application.Response.Meeting
{
    public class MeetingResponse
    {
        public int MeetingId { get; set; }
        public int ProjectId { get; set; }
        public int? TaskId { get; set; }
        public int OrganizerId { get; set; }
        public string? OrganizerName { get; set; }
        public string Subject { get; set; } = null!;
        public string? Agenda { get; set; }
        public DateTime StartDateTime { get; set; }
        public DateTime EndDateTime { get; set; }
        public string TimeZone { get; set; } = "UTC";
        public MeetingStatus Status { get; set; }
        public string? JoinUrl { get; set; }
        public string? ExternalEventId { get; set; }
        public string? ExternalOnlineMeetingId { get; set; }
        public string? FailureReason { get; set; }
        public List<MeetingParticipantResponse> Participants { get; set; } = new List<MeetingParticipantResponse>();
    }

    public class MeetingParticipantResponse
    {
        public int? UserId { get; set; }
        public string Email { get; set; } = null!;
        public string? DisplayName { get; set; }
        public MeetingParticipantRole Role { get; set; }
    }
}
