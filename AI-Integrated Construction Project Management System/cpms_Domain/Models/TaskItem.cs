using System;
using System.Collections.Generic;

namespace cpms_Domain.Models
{
    public class TaskItem : Base
    {
        public int TaskId { get; set; } // TaskID (PK)
        public int ProjectId { get; set; } // ProjectID (FK)
        public string PhaseName { get; set; } = null!;
        public string TaskName { get; set; } = null!;

        // Người chịu trách nhiệm thực hiện
        public int AssignedToUserID { get; set; }
        public virtual UserAccount AssignedToUser { get; set; } = null!;

        // 🚀 TÍNH TOÁN QUẢN TRỊ DỰ ÁN (EVM - Earned Value Management)
        public decimal PlannedBudget { get; set; } // PV - Planned Value (Ngân sách kế hoạch)
        public decimal ActualCost { get; set; }    // AC - Actual Cost (Chi phí thực tế đã chi)

        public DateTime BaselineStart { get; set; }
        public DateTime BaselineEnd { get; set; }
        public decimal ActualProgressPct { get; set; } // 🚀 SỬA: Chuyển sang decimal để đồng bộ tỷ lệ phần trăm (0.00 -> 100.00)
        public TaskStatus Status { get; set; } = TaskStatus.PENDING;

        // Navigation Properties
        public virtual Project Project { get; set; } = null!;
        public virtual ICollection<ProgressReport> ProgressReports { get; set; } = new List<ProgressReport>();
    }

    public enum TaskStatus
    {
        PENDING,
        ACTIVE,
        IN_PROGRESS, // 🚀 BỔ SUNG: Khớp với chữ 'IN_PROGRESS' dưới Database của bạn
        COMPLETED,
        REJECTED
    }
}