using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Application.Response.OrderLineItem
{
    public class OrderLineItemResponse
    {
        public int OrderLineItemId { get; set; }
        public int VariantId { get; set; }
        public int MaterialId { get; set; }
        public int? RequestItemId { get; set; }
        public string MaterialName { get; set; } = null!; // Lấy từ Material entity
        public string VariantName { get; set; } = null!;
        public string Unit { get; set; } = null!;        // Đơn vị tính
        public decimal Quantity { get; set; }
        public decimal ReceivedQuantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal SubTotal => Quantity * UnitPrice; // Tính toán sẵn ở mức DTO
    }
}
