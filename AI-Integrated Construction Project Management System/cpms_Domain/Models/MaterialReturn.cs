namespace cpms_Domain.Models;

public sealed class MaterialReturn : Base
{
    public int ReturnId { get; set; }
    public int MaterialRequestId { get; set; }
    public MaterialRequest MaterialRequest { get; set; } = null!; // Navigation property
    public int WarehouseId { get; set; }
    public int VariantId { get; set; }
    public decimal Quantity { get; set; }
    public string ReasonCode { get; set; } = MaterialReturnReasons.Unused;
    public string Condition { get; set; } = MaterialReturnConditions.Usable;
    public string? Note { get; set; }
    public int RecordedByUserId { get; set; }
    public DateTime ReturnedAt { get; set; }
    public Warehouse Warehouse { get; set; } = null!;
    public MaterialVariant Variant { get; set; } = null!;
    public UserAccount RecordedByUser { get; set; } = null!;
}

public static class MaterialReturnReasons
{
    public const string Unused = "UNUSED";
    public const string ExcessIssue = "EXCESS_ISSUE";
    public const string Damaged = "DAMAGED";
    public static readonly string[] All = [Unused, ExcessIssue, Damaged];
}

public static class MaterialReturnConditions
{
    public const string Usable = "USABLE";
    public const string Quarantined = "QUARANTINED";
    public static readonly string[] All = [Usable, Quarantined];
}
