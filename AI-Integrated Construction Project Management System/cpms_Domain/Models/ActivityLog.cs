using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Domain.Models
{
    public class ActivityLog : Base
    {
        public int Id { get; set; } // Map với LogID trong ERD
        public int UserID { get; set; }
        public string ActivityName { get; set; } = null!;
        public string EntityType { get; set; } = null!;

        // Navigation Property
        public virtual UserAccount User { get; set; } = null!;
    }
}
