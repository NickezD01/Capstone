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
        public int? TaskId { get; set; }
        public string? RequestNote { get; set; }

        /// <summary>
        /// Step 10: PM planning estimate. Must be non-negative; display only and
        /// never debits the project budget.
        /// </summary>
        public decimal EstimatedCost { get; set; }
        public List<MaterialItemRequest> Items { get; set; } = new List<MaterialItemRequest>();
    }

    public class MaterialItemRequest
    {
        public int VariantId { get; set; }
        public int MaterialId { get; set; }
        public decimal Quantity { get; set; }
        public DateTime NeededByDate { get; set; }
        public string? Note { get; set; }
    }
}
