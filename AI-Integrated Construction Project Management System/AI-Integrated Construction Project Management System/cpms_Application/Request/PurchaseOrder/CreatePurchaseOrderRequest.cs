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
        public int WarehouseId { get; set; }
        public DateTime? ExpectedDeliveryDate { get; set; }
        public string? Note { get; set; }
        public List<OrderLineItemDto> Items { get; set; } = new List<OrderLineItemDto>();
    }
    public class OrderLineItemDto
    {
        public int VariantId { get; set; }
        public int MaterialId { get; set; }
        public int? RequestItemId { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }
}
