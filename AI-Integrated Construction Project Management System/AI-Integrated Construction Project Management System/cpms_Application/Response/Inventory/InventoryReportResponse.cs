using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Application.Response.Inventory
{
    public class InventoryReportResponse
    {
        public int InventoryId { get; set; }
        public int WarehouseId { get; set; }
        public int VariantId { get; set; }
        public int MaterialId { get; set; }
        public string MaterialName { get; set; } = null!;
        public string VariantName { get; set; } = null!;
        public string? SKU { get; set; }
        public string? Brand { get; set; }
        public string? Grade { get; set; }
        public string? Size { get; set; }
        public string? Specification { get; set; }
        public string? Packaging { get; set; }
        public string WarehouseName { get; set; } = null!;
        public string Unit { get; set; } = null!;

        // 🚀 ĐỒNG BỘ SỐ LƯỢNG CHI TIẾT THEO ERD
        public decimal QuantityOnHand { get; set; }   // Số lượng thực tế trong kho
        public decimal ReservedQuantity { get; set; }  // Số lượng đã giữ chỗ cho dự án
        public decimal OnOrderQuantity { get; set; }
        public decimal AvailableQuantity { get; set; } // Lượng hàng khả dụng thực tế (= QuantityOnHand - ReservedQuantity)
        public decimal ReorderLevel { get; set; }      // Định mức tối thiểu để báo động nhập hàng
        public decimal QuarantineQuantity { get; set; }
        public decimal AverageUnitCost { get; set; }
        public decimal InventoryValue { get; set; }
        public bool IsLowStock { get; set; }           // Trạng thái cảnh báo hết hàng sắp xảy ra
        public DateTime UpdatedAt { get; set; }
        public string RowVersion { get; set; } = null!;
    }
}
