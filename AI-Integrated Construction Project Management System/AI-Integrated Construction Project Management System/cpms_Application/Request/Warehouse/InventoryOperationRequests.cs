using System.ComponentModel.DataAnnotations;
using cpms_Domain.Models;

namespace cpms_Application.Request.Warehouse
{
    public class InventoryAdjustmentRequest
    {
        public int WarehouseId { get; set; }
        public int VariantId { get; set; }
        public decimal QuantityDelta { get; set; }
        public string ReasonCode { get; set; } = string.Empty;
        public string? Note { get; set; }
        public string? RowVersion { get; set; }
    }

    public class ReviewInventoryAdjustmentRequest
    {
        public string RowVersion { get; set; } = string.Empty;
        public string? ReviewNote { get; set; }
    }

    public class InventoryReturnRequest
    {
        public int WarehouseId { get; set; }
        public int VariantId { get; set; }
        public decimal Quantity { get; set; }
        [Required]
        public int MaterialRequestId { get; set; }
        public string ReasonCode { get; set; } = MaterialReturnReasons.Unused;
        public string Condition { get; set; } = MaterialReturnConditions.Usable;
        public string? Note { get; set; }
        public string? RowVersion { get; set; }
    }

    public class ReceivePurchaseOrderRequest
    {
        public string? Note { get; set; }
        public string? RowVersion { get; set; }
        public bool IsFinalDelivery { get; set; }
        public List<ReceivePurchaseOrderItemRequest> Items { get; set; } = new();
    }

    public class ReceivePurchaseOrderItemRequest
    {
        public int LineItemId { get; set; }
        public decimal Quantity { get; set; }
        public decimal DamagedQuantity { get; set; }
        public decimal MissingQuantity { get; set; }
        public string? LotNumber { get; set; }
        public string? BatchNumber { get; set; }
        public string? SerialNumber { get; set; }
        public DateTime? ExpiryDate { get; set; }
    }
}
