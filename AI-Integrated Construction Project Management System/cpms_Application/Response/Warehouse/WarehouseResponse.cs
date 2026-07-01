using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Application.Response.Warehouse
{
    public class WarehouseResponse
    {
        public int WarehouseId { get; set; }
        public string WarehouseName { get; set; } = null!;
        public string Location { get; set; } = null!;
        public int ManagerId { get; set; }
        public string? ManagerName { get; set; } // Lấy tên thay vì cả object UserAccount

        // Chỉ lấy các trường thuần túy của Inventory, không kéo ngược Warehouse về lại
        public List<InventoryRecordDto> InventoryRecords { get; set; } = new();

        public DateTime CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public int? CreatedBy { get; set; }
        public int? ModifiedBy { get; set; }
        public bool IsDeleted { get; set; }
    }
    public class InventoryRecordDto
    {
        public int InventoryId { get; set; }
        public int MaterialId { get; set; }
        public decimal QuantityOnHand { get; set; }
        public decimal ReservedQuantity { get; set; }
        public decimal ReorderLevel { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
