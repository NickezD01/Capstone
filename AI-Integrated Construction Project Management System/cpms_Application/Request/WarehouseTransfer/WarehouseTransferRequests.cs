namespace cpms_Application.Request.WarehouseTransfer
{
    public class CreateWarehouseTransferRequest
    {
        public int SourceWarehouseId { get; set; }
        public int DestinationWarehouseId { get; set; }
        public string? Note { get; set; }
        public List<CreateWarehouseTransferItemRequest> Items { get; set; } = new();
    }

    public class CreateWarehouseTransferItemRequest
    {
        public int VariantId { get; set; }
        public decimal Quantity { get; set; }
    }

    public class ReceiveWarehouseTransferRequest
    {
        public List<ReceiveWarehouseTransferItemRequest> Items { get; set; } = new();
    }

    public class ReceiveWarehouseTransferItemRequest
    {
        public int TransferItemId { get; set; }
        public decimal Quantity { get; set; }
    }
}
