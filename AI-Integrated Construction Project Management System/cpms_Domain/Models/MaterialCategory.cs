using System;
using System.Collections.Generic;

namespace cpms_Domain.Models;

public partial class MaterialCategory
{
    public long CategoryId { get; set; }

    public string? CategoryName { get; set; }

    public string? Description { get; set; }

    public virtual ICollection<Material> Materials { get; set; } = new List<Material>();
}
