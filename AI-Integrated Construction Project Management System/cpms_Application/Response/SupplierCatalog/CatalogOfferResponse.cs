namespace cpms_Application.Response.SupplierCatalog
{
    public class CatalogOfferResponse
    {
        public int CatalogId { get; set; }
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = null!;
        public int VariantId { get; set; }
        public int MaterialId { get; set; }
        public string MaterialName { get; set; } = null!;
        public string VariantName { get; set; } = null!;
        public string? Sku { get; set; }
        public string? SupplierSku { get; set; }
        public string Unit { get; set; } = null!;
        public decimal UnitPrice { get; set; }
        public decimal MinimumOrderQuantity { get; set; }
        public int LeadTimeDays { get; set; }
        public bool IsAvailable { get; set; }
    }
}
