namespace cpms_Application.Request.MaterialRequest
{
    public class ApproveMaterialRequest
    {
        public string? DecisionNote { get; set; }
        public List<ApproveMaterialItemRequest> Items { get; set; } = new();
    }

    public class ApproveMaterialItemRequest
    {
        public int ItemId { get; set; }
        public decimal ApprovedQuantity { get; set; }

        /// <summary>
        /// Step 10: optional per-line actual unit cost set by the warehouse manager.
        /// When omitted the line falls back to the inventory average at issue time.
        /// </summary>
        public decimal? UnitActualCost { get; set; }
    }

    public class RejectMaterialRequest
    {
        public string? DecisionNote { get; set; }
    }

    public class UpdatePendingMaterialRequest
    {
        public string RowVersion { get; set; } = string.Empty;
        public string? RequestNote { get; set; }

        /// <summary>
        /// Step 10: when supplied, replaces the PM planning estimate. Pending only.
        /// </summary>
        public decimal? EstimatedCost { get; set; }
        public List<UpdateMaterialRequestItem> Items { get; set; } = new();
    }

    public class UpdateMaterialRequestItem
    {
        public int ItemId { get; set; }
        public decimal Quantity { get; set; }
        public DateTime NeededByDate { get; set; }
        public string? Note { get; set; }
    }

    public class CancelMaterialRequest
    {
        public string RowVersion { get; set; } = string.Empty;
        public string? Reason { get; set; }
    }

    /// <summary>
    /// Step 10: warehouse-manager actual-cost adjustment. Only the delta between
    /// the new unit cost and the old unit cost, applied to outstanding (issued
    /// but not returned) quantity, is posted to the budget ledger.
    /// </summary>
    public class AdjustActualCostRequest
    {
        public string RowVersion { get; set; } = string.Empty;
        public string? Note { get; set; }
        public List<AdjustActualCostItemRequest> Items { get; set; } = new();
    }

    public class AdjustActualCostItemRequest
    {
        public int ItemId { get; set; }
        public decimal UnitActualCost { get; set; }
    }

    /// <summary>
    /// Step 10: optional issue parameters. When <see cref="Items"/> is empty the
    /// call issues every active reservation (legacy behavior). Otherwise only the
    /// listed per-line quantities are issued, enabling genuine partial issues.
    /// </summary>
    public class IssueMaterialRequest
    {
        public string? RowVersion { get; set; }
        public List<IssueMaterialItemRequest> Items { get; set; } = new();
    }

    public class IssueMaterialItemRequest
    {
        public int ItemId { get; set; }
        public decimal Quantity { get; set; }
    }
}
