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

        // SỬA: Nên có thêm trường để AI phân tích chi phí
        public decimal TotalAmount { get; set; }
        public DateTime OrderDate { get; set; }
        public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.PENDING;

        // BỔ SUNG: Cho phép theo dõi tiến độ đơn hàng thực tế
        public DateTime? DeliveryDate { get; set; }

        public virtual Project Project { get; set; } = null!;
        public virtual Supplier Supplier { get; set; } = null!;
        public virtual UserAccount UserAccount { get; set; } = null!;
        public virtual ICollection<OrderLineItem> OrderLineItems { get; set; } = new List<OrderLineItem>();
    }
    public enum PurchaseOrderStatus
    {
        PENDING,
        APPROVED,
        REJECTED,
        DELIVERED
    }
}
