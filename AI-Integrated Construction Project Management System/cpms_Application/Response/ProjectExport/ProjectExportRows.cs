namespace cpms_Application.Response.ProjectExport
{
    /// <summary>
    /// Step 13 role-specific project export rows. The full view (owning PM,
    /// assigned customer, admin) contains every sheet; the warehouse-manager
    /// view contains project context plus request, inventory, and movement data.
    /// No user/account administration data is ever included.
    /// </summary>
    public class ProjectExportProjectRow
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public string? Address { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime BaselineStart { get; set; }
        public DateTime BaselineEnd { get; set; }
        public decimal TotalBudget { get; set; }
        public string Currency { get; set; } = string.Empty;
        public string ProjectManager { get; set; } = string.Empty;
        public string Customer { get; set; } = string.Empty;
        public decimal TotalSpent { get; set; }
        public decimal RemainingBudget { get; set; }
    }

    public class ProjectExportPhaseRow
    {
        public int PhaseId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int SequenceOrder { get; set; }
        public DateTime BaselineStart { get; set; }
        public DateTime BaselineEnd { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class ProjectExportTaskRow
    {
        public int TaskId { get; set; }
        public string PhaseName { get; set; } = string.Empty;
        public string TaskName { get; set; } = string.Empty;
        public string AssignedTo { get; set; } = string.Empty;
        public decimal PlannedBudget { get; set; }
        public decimal ActualCost { get; set; }
        public DateTime BaselineStart { get; set; }
        public DateTime BaselineEnd { get; set; }
        public decimal ActualProgressPct { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class ProjectExportRequestRow
    {
        public int RequestId { get; set; }
        public int? TaskId { get; set; }
        public string TaskName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal EstimatedCost { get; set; }
        public decimal ActualCost { get; set; }
        public decimal BudgetDebited { get; set; }
        public DateTime RequestDate { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public string? DecisionNote { get; set; }
    }

    public class ProjectExportRequestLineRow
    {
        public int ItemId { get; set; }
        public int RequestId { get; set; }
        public string MaterialName { get; set; } = string.Empty;
        public string VariantName { get; set; } = string.Empty;
        public string? Unit { get; set; }
        public decimal Quantity { get; set; }
        public decimal ApprovedQuantity { get; set; }
        public decimal IssuedQuantity { get; set; }
        public decimal ReturnedQuantity { get; set; }
        public decimal NetIssuedQuantity { get; set; }
        public decimal UnitActualCost { get; set; }
        public decimal DebitedAmount { get; set; }
    }

    public class ProjectExportLedgerRow
    {
        public int Id { get; set; }
        public string TransactionType { get; set; } = string.Empty;
        public int RequestId { get; set; }
        public int? ItemId { get; set; }
        public decimal Quantity { get; set; }
        public decimal Amount { get; set; }
        public decimal OldActualCost { get; set; }
        public decimal NewActualCost { get; set; }
        public decimal DebitedBefore { get; set; }
        public decimal DebitedAfter { get; set; }
        public int PerformedByUserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? Note { get; set; }
    }

    public class ProjectExportBudgetRow
    {
        public decimal TotalBudget { get; set; }
        public decimal TotalSpent { get; set; }
        public decimal RemainingBudget { get; set; }
        public decimal TotalEstimated { get; set; }
        public decimal TotalActual { get; set; }
        public string Currency { get; set; } = string.Empty;
    }

    public class ProjectExportProgressRow
    {
        public int ReportId { get; set; }
        public int TaskId { get; set; }
        public string TaskName { get; set; } = string.Empty;
        public DateTime ReportDate { get; set; }
        public decimal ProgressIncrement { get; set; }
        public decimal ActualCostIncrement { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime? ReviewedAt { get; set; }
        public string? Notes { get; set; }
    }

    public class ProjectExportInventoryRow
    {
        public int VariantId { get; set; }
        public string MaterialName { get; set; } = string.Empty;
        public string VariantName { get; set; } = string.Empty;
        public string? Unit { get; set; }
        public decimal QuantityOnHand { get; set; }
        public decimal ReservedQuantity { get; set; }
        public decimal AvailableQuantity { get; set; }
        public decimal AverageUnitCost { get; set; }
        public string WarehouseName { get; set; } = string.Empty;
    }

    public class ProjectExportMovementRow
    {
        public long TransactionId { get; set; }
        public string TransactionType { get; set; } = string.Empty;
        public string VariantName { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal QuantityBefore { get; set; }
        public decimal QuantityAfter { get; set; }
        public string Reference { get; set; } = string.Empty;
        public DateTime TransactionDate { get; set; }
        public int PerformedByUserId { get; set; }
    }
}
