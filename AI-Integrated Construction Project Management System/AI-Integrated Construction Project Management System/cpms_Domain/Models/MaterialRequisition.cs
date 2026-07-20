using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Domain.Models
{
    public class MaterialRequisition : Base
    {
        public int ItemId { get; set; } // Map với ItemID (PK)
        public int RequestId { get; set; } // Khóa ngoại trỏ về MaterialsRequests
        public int VariantId { get; set; }
        public decimal Quantity { get; set; }
        public decimal ApprovedQuantity { get; set; }
        public decimal IssuedQuantity { get; set; }
        public DateTime NeededByDate { get; set; }
        public string? Note { get; set; }

        // Navigation Properties
        public virtual MaterialRequest MaterialRequest { get; set; } = null!;
        public virtual MaterialVariant Variant { get; set; } = null!;
        public virtual ICollection<InventoryReservation> Reservations { get; set; } = new List<InventoryReservation>();
        public virtual ICollection<OrderLineItem> OrderLineItems { get; set; } = new List<OrderLineItem>();
    }
}
