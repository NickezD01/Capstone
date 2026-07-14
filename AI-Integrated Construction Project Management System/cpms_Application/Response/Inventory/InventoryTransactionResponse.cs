namespace cpms_Application.Response.Inventory
{
    public class InventoryTransactionResponse
    {
        public long TransactionId { get; set; }
        public int WarehouseId { get; set; }
        public int VariantId { get; set; }
        public string TransactionType { get; set; } = null!;
        public decimal Quantity { get; set; }
        public decimal QuantityBefore { get; set; }
        public decimal QuantityAfter { get; set; }
        public int? ReferenceId { get; set; }
        public string? ReferenceType { get; set; }
        public string? Note { get; set; }
        public int PerformedByUserId { get; set; }
        public DateTime TransactionDate { get; set; }
    }
}
