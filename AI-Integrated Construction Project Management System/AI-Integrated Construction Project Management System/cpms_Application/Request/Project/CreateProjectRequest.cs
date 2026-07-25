using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Application.Request.Project
{
    public class CreateProjectRequest
    {
        public string ProjectName { get; set; } = null!;
        public string? Address { get; set; }
        public decimal TotalProjectBudget { get; set; }

        public DateTime StartDate { get; set; }

        // 🚀 Bổ sung các trường bắt buộc dưới đây
        public int PMUserID { get; set; }
        public DateTime BaselineStart { get; set; }
        public DateTime BaselineEnd { get; set; }
    }
}
