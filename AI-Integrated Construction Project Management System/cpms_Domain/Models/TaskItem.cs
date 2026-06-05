using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Domain.Models
{
    public class TaskItem : Base
    {
        public int TaskId { get; set; }
        public int ProjectId { get; set; }
        public string PhaseName { get; set; } = null!; // Foundation, Finishing
        public string TaskName { get; set; } = null!;  // Pour Concrete
        public DateTime BaselineStart { get; set; }
        public DateTime BaselineEnd { get; set; }
        public int ActualProgressPct { get; set; }     // 0-100
        public TaskStatus Status { get; set; } = TaskStatus.PENDING;

        // Navigation
        public virtual Project Project { get; set; } = null!;
        public virtual ICollection<ProgressReport> ProgressReports { get; set; } = new List<ProgressReport>();
    }
    public enum TaskStatus
    {
        PENDING,
        ACTIVE,
        COMPLETED
    }
}
