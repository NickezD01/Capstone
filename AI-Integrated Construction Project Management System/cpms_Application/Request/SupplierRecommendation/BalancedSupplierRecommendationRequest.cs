namespace cpms_Application.Request.SupplierRecommendation
{
    public class BalancedSupplierRecommendationRequest
    {
        public int? ProjectId { get; set; }
        public List<RequestedMaterialItem> Items { get; set; } = new List<RequestedMaterialItem>();
        public double CostWeight { get; set; } = 0.45;
        public double ReliabilityWeight { get; set; } = 0.45;
        public double LeadTimeWeight { get; set; } = 0.10;
        public int MaxRecommendations { get; set; } = 5;
        public bool SearchWebForNearbySuppliers { get; set; }
        public string? WarehouseLocation { get; set; }
        public int SearchRadiusKm { get; set; } = 30;
        public string? RegionCode { get; set; } = "VN";
    }

    public class RequestedMaterialItem
    {
        public int MaterialId { get; set; }
        public decimal Quantity { get; set; } = 1;
    }
}
