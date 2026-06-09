using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Domain.Models
{
    public class Category
    {
        public int id { get; set; }
        public string CategorylName { get; set; } = null!;
        public virtual ICollection<Material> Materials { get; set; } = new List<Material>();
    }
}
