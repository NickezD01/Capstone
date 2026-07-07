using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Application.Request.MaterialRequest
{
    public class CreateMaterialRequest
    {
        public int ProjectId { get; set; }
        // Thông tin chi tiết các vật tư cần yêu cầu
        public List<MaterialItemRequest> Items { get; set; } = new List<MaterialItemRequest>();
    }

    public class MaterialItemRequest
    {
        public int MaterialId { get; set; }
        public decimal Quantity { get; set; }
        public DateTime NeededByDate { get; set; }
    }
}
