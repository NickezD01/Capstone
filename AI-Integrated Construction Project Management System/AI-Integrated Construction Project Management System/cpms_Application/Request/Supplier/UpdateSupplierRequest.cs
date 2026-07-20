namespace cpms_Application.Request.Supplier;

public sealed class UpdateSupplierRequest
{
    public string CompanyName { get; set; } = string.Empty;
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
}
