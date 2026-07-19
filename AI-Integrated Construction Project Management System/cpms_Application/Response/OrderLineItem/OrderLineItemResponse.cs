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
        public string? SKU { get; set; }
        public string? Brand { get; set; }
        public string? Grade { get; set; }
        public string? Size { get; set; }
        public string? Specification { get; set; }
        public string? Packaging { get; set; }
        public string Unit { get; set; } = null!;        // Đơn vị tính
        public decimal Quantity { get; set; }
        public decimal ReceivedQuantity { get; set; }
        public decimal DamagedQuantity { get; set; }
        public decimal MissingQuantity { get; set; }
        public decimal AccountedQuantity => ReceivedQuantity + DamagedQuantity + MissingQuantity;
        public decimal RemainingQuantity => Math.Max(0, Quantity - AccountedQuantity);
        public decimal UnitPrice { get; set; }
        public decimal SubTotal => Quantity * UnitPrice; // Tính toán sẵn ở mức DTO
    }
}
