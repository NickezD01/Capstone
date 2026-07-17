using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Domain.Models
{
    public class EmailVerification : Base
    {
        public int Id { get; set; }
        public string VerificationCode { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public bool IsUsed { get; set; }
        public string Purpose { get; set; } = SecurityTokenPurposes.EmailVerification;
        public int FailedAttempts { get; set; }

        //navigation property
        public int UserId { get; set; }
        public UserAccount User { get; set; } = null!;
    }

    public static class SecurityTokenPurposes
    {
        public const string EmailVerification = "EMAIL_VERIFICATION";
        public const string PasswordReset = "PASSWORD_RESET";
    }
}
