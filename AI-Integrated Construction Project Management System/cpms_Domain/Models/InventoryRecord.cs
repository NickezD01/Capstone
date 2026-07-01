using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Domain.Models
{
    public class InventoryRecord : Base
    {
        public int InventoryId { get; set; } // InventoryID (PK)
        public int WarehouseId { get; set; } // WarehouseId (FK)
        public int MaterialId { get; set; }  // MaterialId (FK)

        // 🚀 CẬP NHẬT CÁC TRƯỜNG SỐ LƯỢNG CHUẨN ERD
        public decimal QuantityOnHand { get; set; }
        public decimal ReservedQuantity { get; set; }
        public decimal ReorderLevel { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Navigation Properties
        public virtual Warehouse Warehouse { get; set; } = null!;
        public virtual Material Material { get; set; } = null!;
        //public decimal Quantity { get; set; }
    }
}
