using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Application.Response.Inventory
{
    public class InventoryReportResponse
    {
        public string MaterialName { get; set; }
        public string WarehouseName { get; set; }
        public decimal Quantity { get; set; }
        public string Unit { get; set; }
    }
}
