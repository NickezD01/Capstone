using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Application.Request.UserAccount
{
    public class VerificationEmailRequest
    {
        public int UserId { get; set; }
        public string VerificationCode { get; set; } = string.Empty;
    }

    public class ResendVerificationRequest
    {
        public string Email { get; set; } = string.Empty;
    }
}
