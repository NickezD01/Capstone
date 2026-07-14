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
        public int VariantId { get; set; }
        public string? SupplierSku { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal MinimumOrderQuantity { get; set; }
        public int LeadTimeDays { get; set; }
        public bool IsAvailable { get; set; } = true;

        // Navigation
        public virtual Supplier Supplier { get; set; } = null!;
        public virtual MaterialVariant Variant { get; set; } = null!;
    }
}
