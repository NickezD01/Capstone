namespace cpms_Application.Request.Warehouse;

public sealed class StartPhysicalCountRequest
{
    public int WarehouseId { get; set; }
    public List<int> VariantIds { get; set; } = new();
    public string? Note { get; set; }
}

public sealed class SubmitPhysicalCountRequest
{
    public string RowVersion { get; set; } = string.Empty;
    public List<PhysicalCountQuantityRequest> Lines { get; set; } = new();
}

public sealed class PhysicalCountQuantityRequest
{
    public int LineId { get; set; }
    public decimal ActualQuantity { get; set; }
}

public sealed class ReviewPhysicalCountRequest
{
    public string RowVersion { get; set; } = string.Empty;
    public string? ReviewNote { get; set; }
}
