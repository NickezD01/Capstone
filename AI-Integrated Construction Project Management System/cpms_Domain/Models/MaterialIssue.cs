using System;
using System.Collections.Generic;

namespace cpms_Domain.Models;

public partial class MaterialIssue
{
    public long IssueId { get; set; }

    public long? WarehouseId { get; set; }

    public long? ProjectId { get; set; }

    public DateTime? IssueDate { get; set; }

    public virtual ICollection<MaterialIssueDetail> MaterialIssueDetails { get; set; } = new List<MaterialIssueDetail>();

    public virtual Project? Project { get; set; }

    public virtual Warehouse? Warehouse { get; set; }
}
