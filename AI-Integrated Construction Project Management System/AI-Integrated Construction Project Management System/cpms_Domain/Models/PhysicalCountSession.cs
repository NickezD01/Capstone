namespace cpms_Domain.Models;

public sealed class PhysicalCountSession : Base
{
    public int SessionId { get; set; }
    public int WarehouseId { get; set; }
    public string Status { get; set; } = PhysicalCountStatuses.Draft;
    public int CreatedByUserId { get; set; }
    public int? ReviewedByUserId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? Note { get; set; }
    public string? ReviewNote { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public Warehouse Warehouse { get; set; } = null!;
    public UserAccount CreatedByUser { get; set; } = null!;
    public UserAccount? ReviewedByUser { get; set; }
    public ICollection<PhysicalCountLine> Lines { get; set; } = new List<PhysicalCountLine>();
}

public sealed class PhysicalCountLine
{
    public int LineId { get; set; }
    public int SessionId { get; set; }
    public int InventoryId { get; set; }
    public int VariantId { get; set; }
    public decimal ExpectedQuantity { get; set; }
    public byte[] ExpectedInventoryRowVersion { get; set; } = Array.Empty<byte>();
    public decimal? ActualQuantity { get; set; }
    public decimal VarianceQuantity => (ActualQuantity ?? ExpectedQuantity) - ExpectedQuantity;
    public PhysicalCountSession Session { get; set; } = null!;
    public InventoryRecord InventoryRecord { get; set; } = null!;
    public MaterialVariant Variant { get; set; } = null!;
}

public static class PhysicalCountStatuses
{
    public const string Draft = "DRAFT";
    public const string PendingApproval = "PENDING_APPROVAL";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";
}
