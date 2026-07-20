using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Domain.Models
{
    public class Material : Base
    {
        public int MaterialId { get; set; }
        public string MaterialName { get; set; } = null!;
        public string DefaultUnit { get; set; } = null!;
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;

        // SỬA: Thay string bằng Id
        public int CategoryId { get; set; }
        public virtual Category Category { get; set; } = null!;

        // Navigation
        public virtual ICollection<MaterialVariant> Variants { get; set; } = new List<MaterialVariant>();
    }
}
