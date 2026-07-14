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
        public int VariantId { get; set; }

        // Số lượng vật liệu định mức cần thiết để hoàn thành Task này
        public decimal GrossQuantityRequired { get; set; }

        public virtual TaskItem TaskItem { get; set; } = null!;
        public virtual MaterialVariant Variant { get; set; } = null!;
    }
}
