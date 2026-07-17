namespace cpms_Application.Response.WarehouseTransfer
{
    public class WarehouseTransferResponse
    {
        public int TransferId { get; set; }
        public int SourceWarehouseId { get; set; }
        public string SourceWarehouseName { get; set; } = null!;
        public int DestinationWarehouseId { get; set; }
        public string DestinationWarehouseName { get; set; } = null!;
        public string Status { get; set; } = null!;
        public int RequestedByUserId { get; set; }
        public int? ApprovedByUserId { get; set; }
        public int? ShippedByUserId { get; set; }
        public int? ReceivedByUserId { get; set; }
        public DateTime RequestedAt { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public DateTime? ShippedAt { get; set; }
        public DateTime? ReceivedAt { get; set; }
        public string? Note { get; set; }
        public string RowVersion { get; set; } = string.Empty;
        public List<WarehouseTransferItemResponse> Items { get; set; } = new();
    }

    public class WarehouseTransferItemResponse
    {
        public int TransferItemId { get; set; }
        public int VariantId { get; set; }
        public int MaterialId { get; set; }
        public string MaterialName { get; set; } = null!;
        public string VariantName { get; set; } = null!;
        public string Unit { get; set; } = null!;
        public decimal RequestedQuantity { get; set; }
        public decimal ShippedQuantity { get; set; }
        public decimal ReceivedQuantity { get; set; }
    }
}
