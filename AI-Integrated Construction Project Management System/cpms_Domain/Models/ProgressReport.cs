using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Domain.Models
{
    public class ProgressReport : Base
    {
        public int ReportId { get; set; }
        public int TaskId { get; set; }
        public int ReportedByUserId { get; set; }
        public DateTime ReportDate { get; set; }
        public decimal ProgressIncrement { get; set; }
        public string? Notes { get; set; }
        public string? SitePhotoUrl { get; set; }

        // Navigation
        public virtual TaskItem Task { get; set; } = null!;
        public virtual UserAccount Reporter { get; set; } = null!;
    }
}
