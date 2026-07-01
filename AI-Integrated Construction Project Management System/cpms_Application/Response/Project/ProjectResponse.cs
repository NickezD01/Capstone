using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Application.Response.Project
{
    public class ProjectResponse
    {
        // 1. Thông tin cơ bản
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = null!;
        public string? Address { get; set; }
        public string Status { get; set; } = null!;
        public DateTime CreatedDate { get; set; }

        // 2. Thời gian (Cần thiết cho Gantt chart/Timeline)
        public DateTime StartDate { get; set; }
        public DateTime BaselineStart { get; set; }
        public DateTime BaselineEnd { get; set; }

        // 3. Tài chính
        public decimal TotalProjectBudget { get; set; }
        public string Currency { get; set; } = "VND";

        // 4. Thông tin PM (Để FE hiển thị luôn "Quản lý bởi: Nguyễn Văn A")
        public int PMUserID { get; set; }
        public string PMName { get; set; } = null!;

        // 5. Thống kê (Không bắt buộc nhưng FE rất thích)
        public int TotalTasks { get; set; }
        public int TotalAIAlerts { get; set; }
    }
}
