using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Application.Response.MaterialRequest
{
    public class MRPCalculationResponse
    {
        public int MaterialId { get; set; }
        public string MaterialName { get; set; } = null!;
        public decimal TotalGrossRequired { get; set; } // Tổng nhu cầu thô dựa trên lịch trình các Tasks
        public decimal CurrentInventory { get; set; }   // Số lượng hiện có trong kho (InventoryRecord)
        public decimal NetQuantityRequired { get; set; }  // Nhu cầu thực tế cần mua thêm (= Thô - Kho)
        public DateTime EarliestStartDate { get; set; } // Ngày sớm nhất cần vật liệu này dựa trên BaselineStart của Task
    }
}
