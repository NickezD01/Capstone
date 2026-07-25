namespace cpms_Application.Request.SupplierCatalog;

public sealed class UpdateCatalogRequest
{
    public string? SupplierSku { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal MinimumOrderQuantity { get; set; }
    public int LeadTimeDays { get; set; }
    public bool IsAvailable { get; set; }
}
