using System;
using System.Collections.Generic;

namespace cpms_Domain.Models;

public partial class MaterialIssueDetail
{
    public long IssueDetailId { get; set; }

    public long? IssueId { get; set; }

    public long? MaterialId { get; set; }

    public decimal? Quantity { get; set; }

    public virtual MaterialIssue? Issue { get; set; }

    public virtual Material? Material { get; set; }
}
