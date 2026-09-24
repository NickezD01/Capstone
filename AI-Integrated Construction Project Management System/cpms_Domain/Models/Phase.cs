using System;
using System.Collections.Generic;

namespace cpms_Domain.Models
{
    public class Phase : Base
    {
        public int PhaseId { get; set; }
        public int ProjectId { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public int SequenceOrder { get; set; }
        public DateTime BaselineStart { get; set; }
        public DateTime BaselineEnd { get; set; }
        public PhaseStatus Status { get; set; } = PhaseStatus.PLANNED;
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public virtual Project Project { get; set; } = null!;
        public virtual ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();

        public void UpdatePlan(string name, string? description, int sequenceOrder,
            DateTime baselineStart, DateTime baselineEnd)
        {
            if (Status is PhaseStatus.COMPLETED or PhaseStatus.CANCELLED)
                throw new InvalidOperationException("A closed phase cannot be edited.");
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Phase name is required.");
            if (sequenceOrder < 0 || baselineEnd < baselineStart)
                throw new ArgumentException("Phase plan is invalid.");

            Name = name.Trim();
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
            SequenceOrder = sequenceOrder;
            BaselineStart = baselineStart;
            BaselineEnd = baselineEnd;
        }

        public void Cancel()
        {
            if (Status == PhaseStatus.COMPLETED)
                throw new InvalidOperationException("A completed phase cannot be cancelled.");
            if (Status == PhaseStatus.CANCELLED)
                throw new InvalidOperationException("The phase is already cancelled.");
            Status = PhaseStatus.CANCELLED;
        }
    }

    public enum PhaseStatus
    {
        PLANNED,
        IN_PROGRESS,
        COMPLETED,
        CANCELLED
    }
}
