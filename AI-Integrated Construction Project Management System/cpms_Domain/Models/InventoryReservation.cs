namespace cpms_Domain.Models
{
    public class InventoryReservation : Base
    {
        public int ReservationId { get; set; }
        public int InventoryId { get; set; }
        public int RequestId { get; set; }
        public int RequestItemId { get; set; }
        public decimal Quantity { get; set; }
        public string Status { get; set; } = InventoryReservationStatuses.Active;
        public DateTime ReservedAt { get; set; }
        public DateTime? ReleasedAt { get; set; }
        public DateTime? FulfilledAt { get; set; }

        public virtual InventoryRecord InventoryRecord { get; set; } = null!;
        public virtual MaterialRequest MaterialRequest { get; set; } = null!;
        public virtual MaterialRequisition RequestItem { get; set; } = null!;
    }

    public static class InventoryReservationStatuses
    {
        public const string Active = "ACTIVE";
        public const string Released = "RELEASED";
        public const string Fulfilled = "FULFILLED";
    }
}
