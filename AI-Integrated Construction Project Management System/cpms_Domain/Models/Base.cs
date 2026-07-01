using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Domain.Models
{
    public abstract class Base
    {
        public DateTime? CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime? ModifiedDate { get; set; }
        public int? CreatedBy { get; set; } // Sửa: Guid? -> int?
        public int? ModifiedBy { get; set; } // Sửa: Guid? -> int?
        public bool IsDeleted { get; set; }
    }
}
