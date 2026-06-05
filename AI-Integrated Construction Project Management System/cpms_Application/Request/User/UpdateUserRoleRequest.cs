using cpms_Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Application.Request.User
{
    public class UpdateUserRoleRequest
    {
        //public int Id { get; set; }
        public Role Role { get; set; }
    }
}
