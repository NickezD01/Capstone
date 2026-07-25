using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Domain.Models
{
    public class PurchaseOrder : Base
    {
        public int PoId { get; set; }
        public int ProjectId { get; set; }
        public int SupplierId { get; set; }
        public int UserAccountId { get; set; }
        public int WarehouseId { get; set; }

        // SỬA: Nên có thêm trường để AI phân tích chi phí
        public decimal TotalAmount { get; set; }
        public DateTime OrderDate { get; set; }
        public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.PENDING;

        // BỔ SUNG: Cho phép theo dõi tiến độ đơn hàng thực tế
        public DateTime? ExpectedDeliveryDate { get; set; }
        public int? ApprovedByUserId { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public string? Note { get; set; }
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public virtual Project Project { get; set; } = null!;
        public virtual Supplier Supplier { get; set; } = null!;
        public virtual UserAccount UserAccount { get; set; } = null!;
        public virtual UserAccount? Approver { get; set; }
        public virtual Warehouse Warehouse { get; set; } = null!;
        public virtual ICollection<OrderLineItem> OrderLineItems { get; set; } = new List<OrderLineItem>();
    }
    public enum PurchaseOrderStatus
    {
        PENDING,
        APPROVED,
        PROCESSING,
        SHIPPED,
        PARTIALLY_RECEIVED,
        REJECTED,
        DELIVERED,
        CLOSED_WITH_VARIANCE,
        CANCELLED
    }
}
