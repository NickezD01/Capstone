using System;
using System.Collections.Generic;

namespace cpms_Domain.Models;

public partial class PurchaseOrderDetail
{
    public long PodetailId { get; set; }

    public long? Poid { get; set; }

    public long? MaterialId { get; set; }

    public decimal? Quantity { get; set; }

    public decimal? UnitPrice { get; set; }

    public virtual Material? Material { get; set; }

    public virtual PurchaseOrder? Po { get; set; }
}
