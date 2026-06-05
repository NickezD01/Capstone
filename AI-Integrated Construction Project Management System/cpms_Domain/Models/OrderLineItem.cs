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
        public int MaterialId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }

        // Navigation
        public virtual PurchaseOrder PurchaseOrder { get; set; } = null!;
        public virtual Material Material { get; set; } = null!;
    }
}
