using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace cpms_Domain.Models
{
    public class UserAccount : Base
    {
        public int Id { get; set; }
        public byte[] PasswordHash { get; set; }
        public byte[] PasswordSalt { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public bool? IsEmailVerified { get; set; } = false;
        public string? ImgUrl { get; set; }
        public Role Role { get; set; }

        public virtual ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
        public virtual ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new List<PurchaseOrder>();

        public virtual ICollection<ProgressReport> ProgressReports { get; set; } = new List<ProgressReport>();
        public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
        public List<EmailVerification>? EmailVerifications { get; set; }

        //public List<Subscription>? Subscriptions { get; set; }
        //public List<Payment>? Payments { get; set; }
        //public List<Notification>? Notifications { get; set; }
        //public List<Comment> Comments { get; set; }
        //public List<Order>? Orders { get; set; }
    }
    public enum Role
    {
        ADMIN,
        PM,
        ENGINEER
    }
}
