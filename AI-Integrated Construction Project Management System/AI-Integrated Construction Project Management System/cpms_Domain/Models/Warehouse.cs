using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Domain.Models
{
    public class Warehouse : Base
    {
        public int WarehouseId { get; set; } // WarehouseID (PK)
        public string WarehouseName { get; set; } = null!;
        public string Location { get; set; } = null!;

        // 🚀 BỔ SUNG: Khóa ngoại quản lý kho (Trỏ sang bảng Users/UserAccount)
        public int ManagerId { get; set; }

        // Navigation Properties
        public virtual UserAccount Manager { get; set; } = null!;

        // SỬA: Đổi từ Inventories sang InventoryRecords theo ERD
        public virtual ICollection<InventoryRecord> InventoryRecords { get; set; } = new List<InventoryRecord>();
    }
}
