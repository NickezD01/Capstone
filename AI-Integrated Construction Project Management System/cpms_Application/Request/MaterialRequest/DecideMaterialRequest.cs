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

    public class UpdatePendingMaterialRequest
    {
        public string RowVersion { get; set; } = string.Empty;
        public string? RequestNote { get; set; }
        public List<UpdateMaterialRequestItem> Items { get; set; } = new();
    }

    public class UpdateMaterialRequestItem
    {
        public int ItemId { get; set; }
        public decimal Quantity { get; set; }
        public DateTime NeededByDate { get; set; }
        public string? Note { get; set; }
    }

    public class CancelMaterialRequest
    {
        public string RowVersion { get; set; } = string.Empty;
        public string? Reason { get; set; }
    }
}
