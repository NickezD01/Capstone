using System;
using System.Collections.Generic;

namespace cpms_Domain.Models;

public partial class MaterialRequestDetail
{
    public long RequestDetailId { get; set; }

    public long? RequestId { get; set; }

    public long? MaterialId { get; set; }

    public decimal? Quantity { get; set; }

    public virtual Material? Material { get; set; }

    public virtual MaterialRequest? Request { get; set; }
}
