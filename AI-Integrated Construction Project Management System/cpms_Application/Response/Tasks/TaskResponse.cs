using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Application.Response.Tasks
{
    public class TaskResponse
    {
        public int TaskId { get; set; }
        public int ProjectId { get; set; }
        public string PhaseName { get; set; } = null!;
        public string TaskName { get; set; } = null!;
        public int AssignedToUserID { get; set; }
        public string AssignedToUserName { get; set; } = null!;
        public decimal PlannedBudget { get; set; }
        public decimal ActualCost { get; set; }
        public decimal ActualProgressPct { get; set; }
        public string Status { get; set; } = null!;
        public DateTime BaselineStart { get; set; }
        public DateTime BaselineEnd { get; set; }
    }
}
