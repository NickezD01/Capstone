using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Application.Request.Project
{
    public class CreateProjectRequest
    {
        public string ProjectName { get; set; } = null!;
        public string? Address { get; set; }
        public DateTime StartDate { get; set; }
    }
}
