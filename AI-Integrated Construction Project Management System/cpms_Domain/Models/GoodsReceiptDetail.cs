using System;
using System.Collections.Generic;

namespace cpms_Domain.Models;

public partial class GoodsReceiptDetail
{
    public long ReceiptDetailId { get; set; }

    public long? ReceiptId { get; set; }

    public long? MaterialId { get; set; }

    public decimal? Quantity { get; set; }

    public virtual Material? Material { get; set; }

    public virtual GoodsReceipt? Receipt { get; set; }
}
