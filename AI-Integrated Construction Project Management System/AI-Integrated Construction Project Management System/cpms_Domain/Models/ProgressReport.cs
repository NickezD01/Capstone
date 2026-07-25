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
        public decimal ActualCostIncrement { get; set; }
        public string? Notes { get; set; }
        public string? SitePhotoUrl { get; set; }
        public ProgressReportStatus Status { get; set; } = ProgressReportStatus.PENDING;
        public int? ReviewedByUserId { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? ReviewNote { get; set; }
        public int? OriginalReportId { get; set; }
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        // Navigation
        public virtual TaskItem Task { get; set; } = null!;
        public virtual UserAccount Reporter { get; set; } = null!;
        public virtual UserAccount? Reviewer { get; set; }
        public virtual ProgressReport? OriginalReport { get; set; }
    }

    public enum ProgressReportStatus
    {
        PENDING,
        APPROVED,
        REJECTED,
        CORRECTED,
        REVERSED
    }
}
