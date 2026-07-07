using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Application.Response.MaterialRequest
{
    public class MaterialRequestResponse
    {
        public int RequestId { get; set; }
        public int ProjectId { get; set; }
        public int RequestedBy { get; set; }
        public string RequestedByName { get; set; } = null!;
        public DateTime RequestDate { get; set; }
        public string Status { get; set; } = null!;
        public List<MaterialRequisitionDetailResponse> Items { get; set; } = new List<MaterialRequisitionDetailResponse>();
    }

    public class MaterialRequisitionDetailResponse
    {
        public int ItemId { get; set; }
        public int MaterialId { get; set; }
        public string MaterialName { get; set; } = null!;
        public decimal Quantity { get; set; }
        public DateTime NeededByDate { get; set; }
    }
}
