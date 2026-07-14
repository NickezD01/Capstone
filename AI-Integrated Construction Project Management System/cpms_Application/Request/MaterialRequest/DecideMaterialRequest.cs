namespace cpms_Application.Request.MaterialRequest
{
    public class ApproveMaterialRequest
    {
        public int WarehouseId { get; set; }
        public string? DecisionNote { get; set; }
        public List<ApproveMaterialItemRequest> Items { get; set; } = new();
    }

    public class ApproveMaterialItemRequest
    {
        public int ItemId { get; set; }
        public decimal ApprovedQuantity { get; set; }
    }

    public class RejectMaterialRequest
    {
        public string? DecisionNote { get; set; }
    }
}
