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
        public string PhaseName { get; set; } = null!;
        public string TaskName { get; set; } = null!;

        // SỬA: Thêm các cột cho tính toán tài chính
        public decimal PlannedBudget { get; set; } // PV - Planned Value
        public decimal ActualCost { get; set; }    // AC - Actual Cost

        public DateTime BaselineStart { get; set; }
        public DateTime BaselineEnd { get; set; }
        public int ActualProgressPct { get; set; }
        public TaskStatus Status { get; set; } = TaskStatus.PENDING;

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
