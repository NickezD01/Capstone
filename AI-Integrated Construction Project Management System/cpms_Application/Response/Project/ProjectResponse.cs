using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Application.Response.Project
{
    public class ProjectResponse
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = null!;
        public string? Address { get; set; }
        public DateTime? StartDate { get; set; }
        public long? ProjectManagerId { get; set; }
        public long? CustomerId { get; set; }
        public string Status { get; set; } = null!; // Trả về string để FE dễ hiển thị
        public DateTime? CreatedDate { get; set; }
    }
}
