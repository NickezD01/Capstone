using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Domain.Models
{
    public class Project : Base
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = null!;
        public string? Address { get; set; }
        public ProjectStatus Status { get; set; } = ProjectStatus.PLANNING;
        public DateTime BaselineStart { get; set; }
        public DateTime BaselineEnd { get; set; }

        // Navigation
        public virtual ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
        public virtual ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new List<PurchaseOrder>();
    }
    public enum ProjectStatus
    {
        PLANNING,
        IN_PROGRESS,
        COMPLETED,
        DELAYED
    }
}
