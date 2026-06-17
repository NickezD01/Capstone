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
        public string Unit { get; set; } = null!;

        // SỬA: Thay string bằng Id
        public int CategoryId { get; set; }
        public virtual Category Category { get; set; } = null!;

        // Navigation
        public virtual ICollection<SupplierCatalog> SupplierCatalogs { get; set; } = new List<SupplierCatalog>();
        public virtual ICollection<OrderLineItem> OrderLineItems { get; set; } = new List<OrderLineItem>();
        public virtual ICollection<Inventory> Inventories { get; set; } = new List<Inventory>();
    }
}
