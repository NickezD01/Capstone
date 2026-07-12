using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Domain.Models
{
    public class ProjectBudgetHistory
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public decimal AmountChanged { get; set; } // Số tiền nạp thêm (Dương) hoặc giảm đi (Âm)
        public decimal PreviousBudget { get; set; } // Ngân sách trước khi thay đổi (để đối chiếu)
        public decimal NewBudget { get; set; }      // Ngân sách sau khi thay đổi
        public string Reason { get; set; } = string.Empty; // Lý do điều chỉnh
        public int UpdatedByUserId { get; set; }   // ID của Manager thực hiện thay đổi
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Property (Nếu hệ thống của bạn có thiết lập quan hệ)
        public virtual Project? Project { get; set; }
    }
}
