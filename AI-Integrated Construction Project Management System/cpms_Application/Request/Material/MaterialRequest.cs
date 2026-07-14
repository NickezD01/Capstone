using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Application.Request.Material
{
    public class MaterialRequest
    {
        public string MaterialName { get; set; } = null!;
        public string DefaultUnit { get; set; } = null!;
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
        public int CategoryId { get; set; }
    }
}
