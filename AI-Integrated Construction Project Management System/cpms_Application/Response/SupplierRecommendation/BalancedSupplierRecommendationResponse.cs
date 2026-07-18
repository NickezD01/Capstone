namespace cpms_Application.Response.SupplierRecommendation
{
    public class BalancedSupplierRecommendationResponse
    {
        public bool UsedGoogleAI { get; set; }
        public bool UsedWebSearch { get; set; }
        public string Strategy { get; set; } = "Balance cost, reliability, and lead time.";
        public string? AiSummary { get; set; }
        public string? WebSearchSummary { get; set; }
        public List<SupplierRecommendationResponse> Recommendations { get; set; } = new List<SupplierRecommendationResponse>();
    }

    public class SupplierRecommendationResponse
    {
        public int SupplierId { get; set; }
        public string Source { get; set; } = "InternalCatalog";
        public string CompanyName { get; set; } = null!;
        public string? ContactEmail { get; set; }
        public string? ContactPhone { get; set; }
        public string? Address { get; set; }
        public string? WebsiteUrl { get; set; }
        public string? GoogleMapsUrl { get; set; }
        public double? Rating { get; set; }
        public int? ReviewCount { get; set; }
        public string? DistanceEstimate { get; set; }
        public decimal EstimatedTotalCost { get; set; }
        public double AverageLeadTimeDays { get; set; }
        public double ReliabilityScore { get; set; }
        public double DefectRatePct { get; set; }
        public double AvgDeliveryDelay { get; set; }
        public double BalancedScore { get; set; }
        public int MatchedMaterialCount { get; set; }
        public int RequestedMaterialCount { get; set; }
        public string Reason { get; set; } = null!;
        public List<string> SourceUrls { get; set; } = new List<string>();
        public List<SupplierRecommendationLineResponse> Lines { get; set; } = new List<SupplierRecommendationLineResponse>();
    }

    public class SupplierRecommendationLineResponse
    {
        public int MaterialId { get; set; }
        public string MaterialName { get; set; } = null!;
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal EstimatedLineCost { get; set; }
        public int LeadTimeDays { get; set; }
    }
}
