namespace cpms_Domain.Models;

public sealed class InventoryAdjustment : Base
{
    public int AdjustmentId { get; set; }
    public int WarehouseId { get; set; }
    public int VariantId { get; set; }
    public decimal QuantityDelta { get; set; }
    public string ReasonCode { get; set; } = InventoryAdjustmentReasons.CycleCount;
    public string? Note { get; set; }
    public string Status { get; set; } = InventoryAdjustmentStatuses.Pending;
    public int RequestedByUserId { get; set; }
    public int? ReviewedByUserId { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public Warehouse Warehouse { get; set; } = null!;
    public MaterialVariant Variant { get; set; } = null!;
    public UserAccount RequestedByUser { get; set; } = null!;
    public UserAccount? ReviewedByUser { get; set; }
}

public static class InventoryAdjustmentStatuses
{
    public const string Pending = "PENDING";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";
}

public static class InventoryAdjustmentReasons
{
    public const string CycleCount = "CYCLE_COUNT";
    public const string Damage = "DAMAGE";
    public const string Loss = "LOSS";
    public const string DataCorrection = "DATA_CORRECTION";
    public const string OpeningBalance = "OPENING_BALANCE";
    public static readonly string[] All = [CycleCount, Damage, Loss, DataCorrection, OpeningBalance];
}
