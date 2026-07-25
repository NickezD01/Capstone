namespace cpms_Application.Request.Warehouse;

public sealed class UpdateWarehouseRequest
{
    public int ManagerId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
}
