using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Domain.Models
{
    public class AIAlert : Base
    {
        public int Id { get; set; } // Map với AlertID trong ERD
        public int ProjectID { get; set; }
        public string AlertType { get; set; } = null!;
        public string Severity { get; set; } = null!;
        public string Message { get; set; } = null!;
        public DateTime? ResolvedAt { get; set; }

        // Navigation Properties
        public virtual Project Project { get; set; } = null!;
    }
}
