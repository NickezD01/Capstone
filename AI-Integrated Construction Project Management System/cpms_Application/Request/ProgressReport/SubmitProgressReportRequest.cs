using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Application.Request.ProgressReport
{
    public class SubmitProgressReportRequest
    {
        public int TaskId { get; set; }
        public decimal ProgressIncrement { get; set; }
        public decimal ActualCostIncrement { get; set; }
        public string? Notes { get; set; }
        public string? SitePhotoUrl { get; set; }
    }
}
