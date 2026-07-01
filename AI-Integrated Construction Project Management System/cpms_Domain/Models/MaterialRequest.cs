using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Domain.Models
{
    public class MaterialRequest : Base
    {
        public int RequestId { get; set; } // Map với RequestID (PK)
        public int ProjectId { get; set; } // Khóa ngoại trỏ về Projects
        public int RequestedBy { get; set; } // Khóa ngoại trỏ về Users (Người tạo yêu cầu)
        public DateTime RequestDate { get; set; }
        public string Status { get; set; } = "PENDING";

        // Navigation Properties
        public virtual Project Project { get; set; } = null!;
        public virtual UserAccount Requester { get; set; } = null!;

        // Một phiếu yêu cầu tổng sẽ có nhiều dòng vật tư chi tiết bên dưới
        public virtual ICollection<MaterialRequisition> Requisitions { get; set; } = new List<MaterialRequisition>();
    }
}
