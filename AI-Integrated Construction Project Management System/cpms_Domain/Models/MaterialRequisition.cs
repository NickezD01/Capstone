using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Domain.Models
{
    public class MaterialRequisition : Base
    {
        public int ItemId { get; set; } // Map với ItemID (PK)
        public int RequestId { get; set; } // Khóa ngoại trỏ về MaterialsRequests
        public int MaterialId { get; set; } // Khóa ngoại trỏ về Materials
        public decimal Quantity { get; set; }
        public DateTime NeededByDate { get; set; }

        // Navigation Properties
        public virtual MaterialRequest MaterialRequest { get; set; } = null!;
        public virtual Material Material { get; set; } = null!;
    }
}
