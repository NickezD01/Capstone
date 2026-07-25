namespace cpms_Domain.Models
{
    public class WarehouseTransferItem : Base
    {
        public int TransferItemId { get; set; }
        public int TransferId { get; set; }
        public int VariantId { get; set; }
        public decimal RequestedQuantity { get; set; }
        public decimal ShippedQuantity { get; set; }
        public decimal ReceivedQuantity { get; set; }
        public decimal DamagedQuantity { get; set; }
        public decimal LostQuantity { get; set; }
        public decimal UnitCost { get; set; }

        public virtual WarehouseTransfer Transfer { get; set; } = null!;
        public virtual MaterialVariant Variant { get; set; } = null!;
    }
}
