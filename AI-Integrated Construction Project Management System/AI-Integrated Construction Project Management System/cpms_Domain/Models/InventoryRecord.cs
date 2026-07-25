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
        public int VariantId { get; set; }

        // 🚀 CẬP NHẬT CÁC TRƯỜNG SỐ LƯỢNG CHUẨN ERD
        public decimal QuantityOnHand { get; set; }
        public decimal ReservedQuantity { get; set; }
        public decimal OnOrderQuantity { get; set; }
        public decimal ReorderLevel { get; set; }
        public decimal QuarantineQuantity { get; set; }
        public decimal AverageUnitCost { get; set; }
        public decimal InventoryValue { get; private set; }
        public DateTime UpdatedAt { get; set; }
        public byte[] RowVersion { get; set; } = null!;

        public decimal AvailableQuantity { get; private set; }

        // Navigation Properties
        public virtual Warehouse Warehouse { get; set; } = null!;
        public virtual MaterialVariant Variant { get; set; } = null!;
        public virtual ICollection<InventoryReservation> Reservations { get; set; } = new List<InventoryReservation>();
        public virtual ICollection<InventoryTransaction> Transactions { get; set; } = new List<InventoryTransaction>();
    }
}
