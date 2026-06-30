using System;
using System.Collections.Generic;

namespace cpms_Domain.Models;

public partial class PurchaseOrder
{
    public long Poid { get; set; }

    public long? SupplierId { get; set; }

    public DateTime? OrderDate { get; set; }

    public decimal? TotalAmount { get; set; }

    public string? Status { get; set; }

    public virtual ICollection<GoodsReceipt> GoodsReceipts { get; set; } = new List<GoodsReceipt>();

    public virtual ICollection<PurchaseOrderDetail> PurchaseOrderDetails { get; set; } = new List<PurchaseOrderDetail>();

    public virtual Supplier? Supplier { get; set; }
}
