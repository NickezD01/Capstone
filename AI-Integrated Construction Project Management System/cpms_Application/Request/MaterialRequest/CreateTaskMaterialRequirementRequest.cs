using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Application.Request.MaterialRequest
{
    public class CreateTaskMaterialRequirementRequest
    {
        public int MaterialId { get; set; }
        public decimal GrossQuantityRequired { get; set; }
    }
}
