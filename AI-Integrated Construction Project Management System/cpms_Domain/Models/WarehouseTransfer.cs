namespace cpms_Domain.Models
{
    public class WarehouseTransfer : Base
    {
        public int TransferId { get; set; }
        public int SourceWarehouseId { get; set; }
        public int DestinationWarehouseId { get; set; }
        public string Status { get; set; } = WarehouseTransferStatuses.Requested;
        public int RequestedByUserId { get; set; }
        public int? ApprovedByUserId { get; set; }
        public int? ShippedByUserId { get; set; }
        public int? ReceivedByUserId { get; set; }
        public DateTime RequestedAt { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public DateTime? ShippedAt { get; set; }
        public DateTime? ReceivedAt { get; set; }
        public string? Note { get; set; }
        public byte[] RowVersion { get; set; } = null!;

        public virtual Warehouse SourceWarehouse { get; set; } = null!;
        public virtual Warehouse DestinationWarehouse { get; set; } = null!;
        public virtual UserAccount RequestedByUser { get; set; } = null!;
        public virtual UserAccount? ApprovedByUser { get; set; }
        public virtual UserAccount? ShippedByUser { get; set; }
        public virtual UserAccount? ReceivedByUser { get; set; }
        public virtual ICollection<WarehouseTransferItem> Items { get; set; } = new List<WarehouseTransferItem>();
    }

    public static class WarehouseTransferStatuses
    {
        public const string Requested = "REQUESTED";
        public const string Approved = "APPROVED";
        public const string InTransit = "IN_TRANSIT";
        public const string Received = "RECEIVED";
        public const string ClosedWithVariance = "CLOSED_WITH_VARIANCE";
        public const string Rejected = "REJECTED";
        public const string Cancelled = "CANCELLED";
    }
}
