using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Application.Response.ProgressReport
{
    public class ProgressReportResponse
    {
        public int ReportId { get; set; }
        public int TaskId { get; set; }
        public string TaskName { get; set; } = null!;
        public int EngineerId { get; set; }
        public string EngineerName { get; set; } = null!;
        public DateTime ReportDate { get; set; }
        public decimal ProgressIncrement { get; set; }
        public string? Notes { get; set; }
        public string? SitePhotoUrl { get; set; }
    }
}
