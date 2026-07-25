using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Application.Request.Tasks
{
    public class CreateTaskRequest
    {
        public int ProjectId { get; set; }
        public string PhaseName { get; set; } = null!;
        public string TaskName { get; set; } = null!;
        public int AssignedToUserID { get; set; }
        public decimal PlannedBudget { get; set; } // PV
        public DateTime BaselineStart { get; set; }
        public DateTime BaselineEnd { get; set; }
        public List<TaskMaterialRequest> Materials { get; set; } = new List<TaskMaterialRequest>();
    }
    public class TaskMaterialRequest
    {
        public int VariantId { get; set; }
        public int MaterialId { get; set; }
        public decimal GrossQuantityRequired { get; set; }
    }
}
