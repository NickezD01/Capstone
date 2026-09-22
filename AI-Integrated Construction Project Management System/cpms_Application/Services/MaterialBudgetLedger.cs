using cpms_Domain.Models;

namespace cpms_Application.Services
{
    /// <summary>
    /// Step 10 issue-time budget ledger helpers. All entries are append-only;
    /// nothing here ever updates or deletes a <see cref="MaterialBudgetTransaction"/>.
    /// Positive amounts consume project budget, negative amounts restore it.
    /// </summary>
    public static class MaterialBudgetLedger
    {
        public static async Task<MaterialBudgetTransaction> PostAsync(
            IUnitOfWork uow,
            int projectId,
            int requestId,
            int? itemId,
            int? variantId,
            decimal quantity,
            string transactionType,
            decimal amount,
            decimal oldActualCost,
            decimal newActualCost,
            decimal debitedBefore,
            int performedByUserId,
            string? note)
        {
            var entry = new MaterialBudgetTransaction
            {
                ProjectId = projectId,
                RequestId = requestId,
                ItemId = itemId,
                VariantId = variantId,
                Quantity = quantity,
                TransactionType = transactionType,
                Amount = amount,
                OldActualCost = oldActualCost,
                NewActualCost = newActualCost,
                DebitedBefore = debitedBefore,
                DebitedAfter = debitedBefore + amount,
                PerformedByUserId = performedByUserId,
                CreatedAt = DateTime.UtcNow,
                Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim()
            };
            await uow.MaterialBudgetTransactions.AddAsync(entry);
            return entry;
        }

        /// <summary>Authoritative project spend = sum of all ledger amounts.</summary>
        public static async Task<decimal> GetProjectSpendAsync(IUnitOfWork uow, int projectId)
        {
            var entries = await uow.MaterialBudgetTransactions.GetAllAsync(t => t.ProjectId == projectId);
            return entries.Sum(t => t.Amount);
        }

        /// <summary>
        /// Quantity issued but not yet returned for a request line variant.
        /// Counts both tracked returns and legacy return transactions.
        /// </summary>
        public static async Task<decimal> GetOutstandingQuantityAsync(IUnitOfWork uow, int requestId, int variantId)
        {
            var issued = await uow.MaterialRequisitions.GetAllAsync(r => r.RequestId == requestId && r.VariantId == variantId);
            var issuedQty = issued.Sum(r => r.IssuedQuantity);
            var returns = await uow.MaterialReturns.GetAllAsync(r => r.MaterialRequestId == requestId && r.VariantId == variantId);
            var legacy = await uow.InventoryTransactions.GetAllIgnoringQueryFiltersAsync(t =>
                t.TransactionType == InventoryTransactionTypes.Return &&
                t.ReferenceType == "MATERIAL_REQUEST" && t.ReferenceId == requestId && t.VariantId == variantId);
            return Math.Max(0, issuedQty - returns.Sum(r => r.Quantity) - legacy.Sum(t => t.Quantity));
        }

        /// <summary>
        /// Applies a spend delta to the linked task's reported actual cost.
        /// A missing task is skipped so a dangling link never blocks the warehouse;
        /// returns an error message only when the task would go negative.
        /// </summary>
        public static async Task<string?> ApplyTaskActualAsync(IUnitOfWork uow, int? taskId, decimal delta)
        {
            if (!taskId.HasValue || delta == 0) return null;
            var task = await uow.TaskItems.GetByIdAsync(taskId.Value);
            if (task == null) return null;
            if (task.ActualCost + delta < 0) return "The adjustment would drive the task actual cost below zero.";
            task.ActualCost += delta;
            return null;
        }
    }
}
