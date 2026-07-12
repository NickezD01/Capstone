using cpms_Application.Response.OrderLineItem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Application.Response.PurchaseOrder
{
    public class PurchaseOrderResponse
    {
        public int PoId { get; set; }

        // Trả thông tin Project và Supplier dạng Object (hoặc flatten chuỗi tùy ý bạn, ở đây trả Object sẽ đầy đủ nhất)
        public ProjectDto Project { get; set; } = null!;
        public SupplierDto Supplier { get; set; } = null!;

        public string Status { get; set; } = null!;
        public string Currency { get; set; } = "VND"; // Lấy từ Project hoặc mặc định
        public decimal TotalAmount { get; set; }

        public List<OrderLineItemResponse> Items { get; set; } = new();
    }

    // Các class con bổ trợ đi kèm trong detail
    public class ProjectDto
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = null!;
    }

    public class SupplierDto
    {
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = null!;
    }
}
