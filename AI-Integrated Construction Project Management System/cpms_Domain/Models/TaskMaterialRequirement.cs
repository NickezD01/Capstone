using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Domain.Models
{
    public class TaskMaterialRequirement : Base
    {
        public int Id { get; set; }
        public int TaskId { get; set; } // Liên kết tới TaskItem
        public int MaterialId { get; set; } // Liên kết tới vật liệu cần dùng

        // Số lượng vật liệu định mức cần thiết để hoàn thành Task này
        public decimal GrossQuantityRequired { get; set; }

        public virtual TaskItem TaskItem { get; set; } = null!;
        public virtual Material Material { get; set; } = null!;
    }
}
