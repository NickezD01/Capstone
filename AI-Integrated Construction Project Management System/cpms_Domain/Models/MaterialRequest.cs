using System;
using System.Collections.Generic;

namespace cpms_Domain.Models;

public partial class MaterialRequest
{
    public long RequestId { get; set; }

    public long? ProjectId { get; set; }

    public long? UserId { get; set; }

    public DateTime? RequestDate { get; set; }

    public string? Status { get; set; }

    public virtual ICollection<MaterialRequestDetail> MaterialRequestDetails { get; set; } = new List<MaterialRequestDetail>();

    public virtual Project? Project { get; set; }

    public virtual User? User { get; set; }
}
