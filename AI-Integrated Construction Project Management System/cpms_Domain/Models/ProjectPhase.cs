using System;

namespace cpms_Domain.Models
{
    public class ProjectPhase : Base
    {
        public int ProjectPhaseId { get; set; }
        public int ProjectId { get; set; }
        public string PhaseName { get; set; } = null!;
        public string? Description { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        // Navigation
        public virtual Project Project { get; set; } = null!;
        public virtual ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
    }
}
