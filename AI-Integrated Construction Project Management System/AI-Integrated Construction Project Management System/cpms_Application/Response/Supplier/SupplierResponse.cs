namespace cpms_Application.Response.Supplier
{
    public class SupplierResponse
    {
        public int SupplierId { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string? ContactEmail { get; set; }
        public string? ContactPhone { get; set; }
    }
}
