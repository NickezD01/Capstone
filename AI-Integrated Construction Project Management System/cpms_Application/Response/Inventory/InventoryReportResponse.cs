using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Application.Response.Inventory
{
    public class InventoryReportResponse
    {
        public int MaterialId { get; set; }
        public string MaterialName { get; set; } = null!;
        public string WarehouseName { get; set; } = null!;
        public string Unit { get; set; } = null!;

        // 🚀 ĐỒNG BỘ SỐ LƯỢNG CHI TIẾT THEO ERD
        public decimal QuantityOnHand { get; set; }   // Số lượng thực tế trong kho
        public decimal ReservedQuantity { get; set; }  // Số lượng đã giữ chỗ cho dự án
        public decimal AvailableQuantity { get; set; } // Lượng hàng khả dụng thực tế (= QuantityOnHand - ReservedQuantity)
        public decimal ReorderLevel { get; set; }      // Định mức tối thiểu để báo động nhập hàng
        public bool IsLowStock { get; set; }           // Trạng thái cảnh báo hết hàng sắp xảy ra
        public DateTime UpdatedAt { get; set; }
    }
}
