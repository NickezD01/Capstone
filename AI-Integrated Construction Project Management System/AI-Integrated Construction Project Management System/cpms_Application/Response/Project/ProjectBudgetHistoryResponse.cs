using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Application.Response.Project
{
    public class ProjectBudgetHistoryResponse
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public decimal AmountChanged { get; set; }
        public decimal PreviousBudget { get; set; }
        public decimal NewBudget { get; set; }
        public string Currency { get; set; } = "VND";
        public string Reason { get; set; } = string.Empty;
        public int UpdatedByUserId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
