using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Application.Request.Material
{
    public class UpdateMaterialRequest
    {
        public string MaterialName { get; set; } = null!;
        public string Unit { get; set; } = null!;
        //public int CategoryId { get; set; }
    }
}
