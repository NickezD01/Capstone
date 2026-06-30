using System;
using System.Collections.Generic;

namespace cpms_Domain.Models;

public partial class GoodsReceipt
{
    public long ReceiptId { get; set; }

    public long? Poid { get; set; }

    public long? WarehouseId { get; set; }

    public DateTime? ReceiptDate { get; set; }

    public virtual ICollection<GoodsReceiptDetail> GoodsReceiptDetails { get; set; } = new List<GoodsReceiptDetail>();

    public virtual PurchaseOrder? Po { get; set; }

    public virtual Warehouse? Warehouse { get; set; }
}
