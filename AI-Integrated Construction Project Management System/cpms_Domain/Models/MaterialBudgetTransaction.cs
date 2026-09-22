using System;

namespace cpms_Domain.Models
{
    /// <summary>
    /// Step 10 immutable issue-time budget ledger. Every material-issue debit,
    /// actual-cost correction delta, and return reversal is recorded here exactly
    /// once. Entries are append-only: no service may update or delete them.
    /// Positive <see cref="Amount"/> consumes project budget; negative restores it.
    /// </summary>
    public class MaterialBudgetTransaction
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public int RequestId { get; set; }
        public int? ItemId { get; set; }
        public int? VariantId { get; set; }
        public decimal Quantity { get; set; }
        public string TransactionType { get; set; } = MaterialBudgetTransactionTypes.IssueDebit;
        public decimal Amount { get; set; }
        public decimal OldActualCost { get; set; }
        public decimal NewActualCost { get; set; }
        public decimal DebitedBefore { get; set; }
        public decimal DebitedAfter { get; set; }
        public int PerformedByUserId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? Note { get; set; }

        public virtual Project Project { get; set; } = null!;
        public virtual MaterialRequest MaterialRequest { get; set; } = null!;
    }

    public static class MaterialBudgetTransactionTypes
    {
        public const string IssueDebit = "ISSUE_DEBIT";
        public const string CorrectionDelta = "CORRECTION_DELTA";
        public const string ReturnReversal = "RETURN_REVERSAL";
    }
}
