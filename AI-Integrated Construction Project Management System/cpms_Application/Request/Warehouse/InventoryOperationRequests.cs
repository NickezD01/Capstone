using System.ComponentModel.DataAnnotations;

namespace cpms_Application.Request.Warehouse
{
    public class InventoryAdjustmentRequest
    {
        public int WarehouseId { get; set; }
        public int VariantId { get; set; }
        public decimal QuantityDelta { get; set; }
        public string? Note { get; set; }
        public string? RowVersion { get; set; }
    }

    public class InventoryReturnRequest
    {
        public int WarehouseId { get; set; }
        public int VariantId { get; set; }
        public decimal Quantity { get; set; }
        [Required]
        public int MaterialRequestId { get; set; }
        public string? Note { get; set; }
        public string? RowVersion { get; set; }
    }

    public class ReceivePurchaseOrderRequest
    {
        public string? Note { get; set; }
        public List<ReceivePurchaseOrderItemRequest> Items { get; set; } = new();
    }

    public class ReceivePurchaseOrderItemRequest
    {
        public int LineItemId { get; set; }
        public decimal Quantity { get; set; }
    }
}
