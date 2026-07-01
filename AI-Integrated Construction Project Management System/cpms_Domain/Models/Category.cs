using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Domain.Models
{
    public class Category
    {
        public int Id { get; set; } // SỬA: Từ id sang Id
        public string CategoryName { get; set; } = null!; // SỬA: Lỗi chính tả CategorylName -> CategoryName
        public virtual ICollection<Material> Materials { get; set; } = new List<Material>();
    }
}
