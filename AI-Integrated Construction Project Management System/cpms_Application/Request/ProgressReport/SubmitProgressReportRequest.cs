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
        public int ProgressIncrement { get; set; } // % tiến độ nộp thêm lượt này (Ví dụ: 10.5)
        public string? Notes { get; set; }
        public string? SitePhotoUrl { get; set; }
    }
}
