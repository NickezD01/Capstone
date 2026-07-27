using System;
using System.Collections.Generic;

namespace cpms_Domain.Models
{
    public class Meeting : Base
    {
        public int MeetingId { get; set; }
        public int ProjectId { get; set; }
        public int? TaskId { get; set; }
        public int OrganizerId { get; set; }
        public string Subject { get; set; } = null!;
        public string? Agenda { get; set; }
        public DateTime StartDateTime { get; set; }
        public DateTime EndDateTime { get; set; }
        public string TimeZone { get; set; } = "UTC";
        public MeetingProvider Provider { get; set; } = MeetingProvider.MICROSOFT_TEAMS;
        public MeetingStatus Status { get; set; } = MeetingStatus.DRAFT;
        public string? JoinUrl { get; set; }
        public string? ExternalEventId { get; set; }
        public string? ExternalOnlineMeetingId { get; set; }
        public string? GraphResponse { get; set; }
        public string? FailureReason { get; set; }

        public virtual Project Project { get; set; } = null!;
        public virtual TaskItem? Task { get; set; }
        public virtual UserAccount Organizer { get; set; } = null!;
        public virtual ICollection<MeetingParticipant> Participants { get; set; } = new List<MeetingParticipant>();
    }

    public enum MeetingProvider
    {
        MICROSOFT_TEAMS
    }

    public enum MeetingStatus
    {
        DRAFT,
        SCHEDULED,
        FAILED,
        CANCELLED
    }
}
