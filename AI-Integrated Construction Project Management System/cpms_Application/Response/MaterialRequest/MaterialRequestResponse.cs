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
        public int? TaskId { get; set; }
        public int? WarehouseId { get; set; }
        public string? WarehouseName { get; set; }
        public int RequestedBy { get; set; }
        public string RequestedByName { get; set; } = null!;
        public DateTime RequestDate { get; set; }
        public string Status { get; set; } = null!;
        public string? RequestNote { get; set; }
        public int? ApprovedByUserId { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public string? DecisionNote { get; set; }
        public List<MaterialRequisitionDetailResponse> Items { get; set; } = new List<MaterialRequisitionDetailResponse>();
    }

    public class MaterialRequisitionDetailResponse
    {
        public int ItemId { get; set; }
        public int VariantId { get; set; }
        public int MaterialId { get; set; }
        public string MaterialName { get; set; } = null!;
        public string VariantName { get; set; } = null!;
        public string? Unit { get; set; }
        public decimal Quantity { get; set; }
        public decimal ApprovedQuantity { get; set; }
        public decimal IssuedQuantity { get; set; }
        public DateTime NeededByDate { get; set; }
        public string? Note { get; set; }
    }
}
