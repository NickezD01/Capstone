using System;
using System.Collections.Generic;

namespace cpms_Domain.Models
{
    public class TaskItem : Base
    {
        public int TaskId { get; set; } // TaskID (PK)
        public int ProjectId { get; set; } // ProjectID (FK)
        public int ProjectPhaseId { get; set; } // FK to ProjectPhase
        public string TaskName { get; set; } = null!;

        // Người chịu trách nhiệm thực hiện
        public int AssignedToUserID { get; set; }
        public virtual UserAccount AssignedToUser { get; set; } = null!;
        public virtual ProjectPhase ProjectPhase { get; set; } = null!;

        // 🚀 TÍNH TOÁN QUẢN TRỊ DỰ ÁN (EVM - Earned Value Management)
        public decimal PlannedBudget { get; set; } // PV - Planned Value (Ngân sách kế hoạch)
        public decimal ActualCost { get; set; }    // AC - Actual Cost (Chi phí thực tế đã chi)

        public DateTime BaselineStart { get; set; }
        public DateTime BaselineEnd { get; set; }
        public decimal ActualProgressPct { get; set; } // 🚀 SỬA: Chuyển sang decimal để đồng bộ tỷ lệ phần trăm (0.00 -> 100.00)
        public TaskStatus Status { get; set; } = TaskStatus.PENDING;
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        // Navigation Properties
        public virtual Project Project { get; set; } = null!;
        public virtual ICollection<ProgressReport> ProgressReports { get; set; } = new List<ProgressReport>();
        public virtual ICollection<TaskMaterialRequirement> MaterialRequirements { get; set; } = new List<TaskMaterialRequirement>();

        public void UpdatePlan(int projectPhaseId, string taskName, int assigneeId, decimal plannedBudget,
            DateTime baselineStart, DateTime baselineEnd)
        {
            if (Status is TaskStatus.COMPLETED or TaskStatus.CANCELLED)
                throw new InvalidOperationException("A closed task cannot be edited.");
            if (plannedBudget < 0 || baselineEnd < baselineStart) throw new ArgumentException("Task plan is invalid.");
            ProjectPhaseId = projectPhaseId;
            TaskName = taskName.Trim();
            AssignedToUserID = assigneeId;
            PlannedBudget = plannedBudget;
            BaselineStart = baselineStart;
            BaselineEnd = baselineEnd;
        }

        public void Cancel()
        {
            if (Status == TaskStatus.COMPLETED) throw new InvalidOperationException("A completed task cannot be cancelled.");
            Status = TaskStatus.CANCELLED;
        }

        public void Reject()
        {
            if (Status != TaskStatus.PENDING) throw new InvalidOperationException("Only a pending task can be rejected.");
            Status = TaskStatus.REJECTED;
        }

        public void Reopen()
        {
            if (Status is not (TaskStatus.REJECTED or TaskStatus.CANCELLED))
                throw new InvalidOperationException("Only a rejected or cancelled task can be reopened.");
            Status = TaskStatus.PENDING;
        }
    }

    public enum TaskStatus
    {
        PENDING,
        ACTIVE, // Legacy database value; new progress updates use IN_PROGRESS.
        IN_PROGRESS,
        COMPLETED,
        REJECTED,
        CANCELLED
    }
}
