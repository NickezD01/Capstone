using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Application.Response.PurchaseOrder
{
    public class BudgetStatusResponse
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public decimal TotalProjectBudget { get; set; }
        public decimal TotalOrderedAmount { get; set; }
        public decimal Variance { get; set; } // > 0 là dư, < 0 là thiếu (vượt)
        public string Message { get; set; } = string.Empty;   // Thông báo chi tiết (Ví dụ: "Dự án đang dư 5,000,000 VND")
        public bool IsOverBudget { get; set; } // Trạng thái cảnh báo để Frontend bôi đỏ
    }
}
