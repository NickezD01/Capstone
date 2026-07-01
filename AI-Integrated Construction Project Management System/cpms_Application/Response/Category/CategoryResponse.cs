using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Application.Response.Category
{
    public class CategoryResponse
    {
        public int Id { get; set; }
        public string CategoryName { get; set; } = null!;
        public int TotalMaterials { get; set; } // Trả thêm số lượng vật tư thuộc danh mục này (FE rất thích)
    }
}
