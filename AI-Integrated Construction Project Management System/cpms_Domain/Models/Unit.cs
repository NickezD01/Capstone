using System;
using System.Collections.Generic;

namespace cpms_Domain.Models;

public partial class Unit
{
    public long UnitId { get; set; }

    public string? UnitName { get; set; }

    public virtual ICollection<Material> Materials { get; set; } = new List<Material>();
}
