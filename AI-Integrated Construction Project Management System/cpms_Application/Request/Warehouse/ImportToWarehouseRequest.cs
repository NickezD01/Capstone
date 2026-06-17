using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Application.Request.Warehouse
{
    public class ImportToWarehouseRequest
    {
        public int PoId { get; set; }
        public int WarehouseId { get; set; }
    }
}
