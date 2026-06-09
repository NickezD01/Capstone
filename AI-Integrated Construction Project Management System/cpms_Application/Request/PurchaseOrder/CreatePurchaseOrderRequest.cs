using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Application.Request.PurchaseOrder
{
    public class CreatePurchaseOrderRequest
    {
        public int ProjectId { get; set; }
        public int SupplierId { get; set; }
        public List<OrderLineItemDto> Items { get; set; }
    }
    public class OrderLineItemDto
    {
        public int MaterialId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }
}
