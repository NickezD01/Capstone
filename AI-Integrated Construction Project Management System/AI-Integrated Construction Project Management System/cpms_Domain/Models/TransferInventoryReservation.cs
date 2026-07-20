namespace cpms_Domain.Models;

public sealed class TransferInventoryReservation
{
    public int TransferReservationId { get; set; }
    public int TransferId { get; set; }
    public int TransferItemId { get; set; }
    public int InventoryId { get; set; }
    public decimal Quantity { get; set; }
    public string Status { get; set; } = TransferReservationStatuses.Active;
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public WarehouseTransfer Transfer { get; set; } = null!;
    public WarehouseTransferItem TransferItem { get; set; } = null!;
    public InventoryRecord Inventory { get; set; } = null!;
}

public static class TransferReservationStatuses
{
    public const string Active = "ACTIVE";
    public const string Consumed = "CONSUMED";
    public const string Released = "RELEASED";
}
