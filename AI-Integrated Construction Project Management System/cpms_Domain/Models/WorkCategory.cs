using System;
using System.Collections.Generic;

namespace cpms_Domain.Models
{
    /// <summary>
    /// Global work-category lookup grouping phases (Structural, Finishing, ...).
    /// Admin-managed reference data; phases must reference an existing category.
    /// </summary>
    public class WorkCategory : Base
    {
        public int WorkCategoryId { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }

        public virtual ICollection<Phase> Phases { get; set; } = new List<Phase>();
    }
}
