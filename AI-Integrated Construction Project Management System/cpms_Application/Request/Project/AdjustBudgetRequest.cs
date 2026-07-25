using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Application.Request.Project
{
    public class AdjustBudgetRequest
    {
        public int ProjectId { get; set; }
        public decimal Amount { get; set; } // Số tiền nạp thêm (Dương) hoặc giảm đi (Âm)
        public string Reason { get; set; } = string.Empty; // Lý do điều chỉnh ngân sách
    }
}
