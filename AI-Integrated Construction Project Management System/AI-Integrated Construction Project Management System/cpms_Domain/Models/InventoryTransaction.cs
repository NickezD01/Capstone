namespace cpms_Domain.Models
{
    public class InventoryTransaction
    {
        public long TransactionId { get; set; }
        public int InventoryId { get; set; }
        public int VariantId { get; set; }
        public int WarehouseId { get; set; }
        public string TransactionType { get; set; } = null!;
        public decimal Quantity { get; set; }
        public decimal QuantityBefore { get; set; }
        public decimal QuantityAfter { get; set; }
        public int? ReferenceId { get; set; }
        public string? ReferenceType { get; set; }
        public string? Note { get; set; }
        public int PerformedByUserId { get; set; }
        public DateTime TransactionDate { get; set; }
        public decimal? UnitCost { get; set; }
        public decimal? TotalValue { get; set; }
        public string? LotNumber { get; set; }
        public string? BatchNumber { get; set; }
        public string? SerialNumber { get; set; }
        public DateTime? ExpiryDate { get; set; }

        public virtual InventoryRecord InventoryRecord { get; set; } = null!;
        public virtual MaterialVariant Variant { get; set; } = null!;
        public virtual Warehouse Warehouse { get; set; } = null!;
        public virtual UserAccount PerformedBy { get; set; } = null!;
    }

    public static class InventoryTransactionTypes
    {
        public const string Receipt = "RECEIPT";
        public const string Issue = "ISSUE";
        public const string Return = "RETURN";
        public const string Adjustment = "ADJUSTMENT";
        public const string TransferOut = "TRANSFER_OUT";
        public const string TransferIn = "TRANSFER_IN";
        public const string PhysicalCount = "PHYSICAL_COUNT";
    }
}
