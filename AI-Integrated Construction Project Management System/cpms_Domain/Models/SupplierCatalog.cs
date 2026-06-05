using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Domain.Models
{
    public class SupplierCatalog : Base
    {
        public int CatalogId { get; set; }
        public int SupplierId { get; set; }
        public int MaterialId { get; set; }
        public decimal UnitPrice { get; set; }
        public int LeadTimeDays { get; set; }

        // Navigation
        public virtual Supplier Supplier { get; set; } = null!;
        public virtual Material Material { get; set; } = null!;
    }
}
