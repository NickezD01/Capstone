using System;

namespace cpms_Domain.Models
{
    /// <summary>
    /// Work problem reported on a task. Created by the owning PM or the
    /// assigned site worker; resolved only by the owning PM.
    /// </summary>
    public class TaskIssue : Base
    {
        public int IssueId { get; set; }
        public int TaskId { get; set; }
        public int ReportedByUserId { get; set; }
        public string Description { get; set; } = null!;
        public string? PhotoUrl { get; set; }
        public TaskIssueStatus Status { get; set; } = TaskIssueStatus.OPEN;
        public string? ResolutionNote { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ResolvedAt { get; set; }
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public virtual TaskItem Task { get; set; } = null!;
        public virtual UserAccount ReportedByUser { get; set; } = null!;
    }

    public enum TaskIssueStatus
    {
        OPEN,
        RESOLVED
    }
}
