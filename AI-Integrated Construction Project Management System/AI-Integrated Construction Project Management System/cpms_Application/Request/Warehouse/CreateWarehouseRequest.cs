using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Application.Request.Warehouse
{
    public class CreateWarehouseRequest
    {
        public int ManagerId { get; set; }
        public string WarehouseName { get; set; } = null!;
        public string Location { get; set; } = null!;
    }
}
