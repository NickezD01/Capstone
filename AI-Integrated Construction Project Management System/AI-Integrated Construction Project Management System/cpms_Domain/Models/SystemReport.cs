using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Domain.Models
{
    public class SystemReport : Base
    {
        public int Id { get; set; } // Map với ReportID trong ERD
        public int ProjectID { get; set; }
        public int GeneratedBy { get; set; } // Khóa ngoại trỏ về UserAccount (UserID)
        public string ReportType { get; set; } = null!;

        // Navigation Properties
        public virtual Project Project { get; set; } = null!;
        public virtual UserAccount Generator { get; set; } = null!;
    }
}
