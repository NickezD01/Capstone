using System;
using cpms_Domain.Models;

namespace cpms_Domain.Ledger
{
    public class ProjectBudgetLedger : Base
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public virtual Project Project { get; set; } = null!;
        public int MaterialRequestId { get; set; }
        public virtual MaterialRequest MaterialRequest { get; set; } = null!;

        // Snapshot of the request values that caused this immutable movement.
        public decimal EstimatedCost { get; set; }
        public decimal ActualCost { get; set; }
        public decimal BudgetDebitedAmount { get; set; }
        public string EntryType { get; set; } = ProjectBudgetLedgerEntryTypes.Issue;
        public int RecordedByUserId { get; set; }
        public DateTime RecordedAt { get; set; }
        public string Note { get; set; } = string.Empty;
    }

    public static class ProjectBudgetLedgerEntryTypes
    {
        public const string Issue = "ISSUE";
        public const string Correction = "CORRECTION";
        public const string Return = "RETURN";
    }
}
