using System;
using cpms_Domain.Models;

namespace cpms_Domain.Ledger
{
    public class ProjectBudgetLedger : Base
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public virtual Project Project { get; set; } = null!;
        
        // Planning/Estimates
        public decimal EstimatedCost { get; set; }
        
        // Actuals
        public decimal ActualCost { get; set; }
        public DateTime ActualCostUpdatedAt { get; set; }
        public int ActualCostUpdatedAtByUserId { get; set; }
        
        // Ledger entry
        public decimal BudgetDebitedAmount { get; set; }
        
        public string Note { get; set; } = string.Empty;
    }
}
