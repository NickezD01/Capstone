using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Application.Request.SupplierCatalog
{
    public class CreateCatalogRequest
    {
        public int SupplierId { get; set; }
        public int MaterialId { get; set; }
        public decimal UnitPrice { get; set; }
        public int LeadTimeDays { get; set; }
    }
}
