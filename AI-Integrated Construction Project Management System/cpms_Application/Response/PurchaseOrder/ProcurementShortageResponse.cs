namespace cpms_Application.Response.PurchaseOrder
{
    public class ProcurementShortageResponse
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = null!;
        public int? TaskId { get; set; }
        public int WarehouseId { get; set; }
        public string WarehouseName { get; set; } = null!;
        public int RequestItemId { get; set; }
        public List<int> RequestIds { get; set; } = new();
        public int VariantId { get; set; }
        public int MaterialId { get; set; }
        public string MaterialName { get; set; } = null!;
        public string VariantName { get; set; } = null!;
        public string? Sku { get; set; }
        public string Unit { get; set; } = null!;
        public DateTime NeededByDate { get; set; }
        public decimal GrossShortageQuantity { get; set; }
        public decimal ProcurementCoverageQuantity { get; set; }
        public decimal RemainingShortageQuantity { get; set; }
        public List<ProcurementOfferResponse> SupplierOffers { get; set; } = new();
    }

    public class ProcurementOfferResponse
    {
        public int CatalogId { get; set; }
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = null!;
        public string? SupplierSku { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal MinimumOrderQuantity { get; set; }
        public int LeadTimeDays { get; set; }
        public DateTime EarliestDeliveryDate { get; set; }
        public decimal SuggestedOrderQuantity { get; set; }
        public decimal ExpectedExcessStockQuantity { get; set; }
        public decimal SuggestedOrderTotal { get; set; }
    }
}
