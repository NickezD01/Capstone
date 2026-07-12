namespace cpms_Domain.Models
{
    public class MeetingParticipant : Base
    {
        public int ParticipantId { get; set; }
        public int MeetingId { get; set; }
        public int? UserId { get; set; }
        public string Email { get; set; } = null!;
        public string? DisplayName { get; set; }
        public MeetingParticipantRole Role { get; set; } = MeetingParticipantRole.REQUIRED;

        public virtual Meeting Meeting { get; set; } = null!;
        public virtual UserAccount? User { get; set; }
    }

    public enum MeetingParticipantRole
    {
        REQUIRED,
        OPTIONAL
    }
}
