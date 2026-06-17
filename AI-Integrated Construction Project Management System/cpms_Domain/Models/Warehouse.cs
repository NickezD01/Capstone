using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Domain.Models
{
    public class Warehouse : Base
    {
        public int WarehouseId { get; set; }
        public string WarehouseName { get; set; } = null!;
        public string Location { get; set; } = null!;

        // Navigation: Danh sách vật liệu đang có trong kho này
        public virtual ICollection<Inventory> Inventories { get; set; } = new List<Inventory>();
    }

    public class Inventory : Base
    {
        public int InventoryId { get; set; }
        public int WarehouseId { get; set; }
        public int MaterialId { get; set; }
        public decimal Quantity { get; set; } // Số lượng tồn kho

        public virtual Warehouse Warehouse { get; set; } = null!;
        public virtual Material Material { get; set; } = null!;
    }
}
