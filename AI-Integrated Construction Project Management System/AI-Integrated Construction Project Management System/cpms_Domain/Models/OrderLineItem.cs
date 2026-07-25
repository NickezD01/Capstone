using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Domain.Models
{
    public class OrderLineItem : Base
    {
        public int LineItemId { get; set; }
        public int PoId { get; set; }
        public int VariantId { get; set; }
        public int? RequestItemId { get; set; }

        // SỬA: Dùng decimal cho Quantity
        public decimal Quantity { get; set; }
        public decimal ReceivedQuantity { get; set; }
        public decimal DamagedQuantity { get; set; }
        public decimal MissingQuantity { get; set; }
        public decimal UnitPrice { get; set; }

        // TÍNH TOÁN: Trường này có thể là Read-only property
        public decimal SubTotal => Quantity * UnitPrice;

        public virtual PurchaseOrder PurchaseOrder { get; set; } = null!;
        public virtual MaterialVariant Variant { get; set; } = null!;
        public virtual MaterialRequisition? RequestItem { get; set; }
    }
}
