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
        public string SupplierName { get; set; } = null!;
        public string Status { get; set; } = null!;
        public decimal TotalAmount { get; set; }
        public List<OrderLineItemResponse> Items { get; set; } = new();
    }
}
