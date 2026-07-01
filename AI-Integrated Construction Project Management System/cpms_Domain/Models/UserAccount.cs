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
        public byte[] PasswordHash { get; set; } = null!;
        public byte[] PasswordSalt { get; set; } = null!;
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public bool? IsEmailVerified { get; set; } = false;
        public string? ImgUrl { get; set; }
        public Role Role { get; set; }

        // ==========================================
        // RELATIONSHIPS MAP THEO ĐÚNG ERD
        // ==========================================

        // 1. Một User với vai trò PM có thể quản lý nhiều Dự án (FK: PMUserID trong Projects)
        public virtual ICollection<Project> ManagedProjects { get; set; } = new List<Project>();

        // 2. Một User (Engineer) được giao nhiều Tasks (FK: AssignedToUserID trong Tasks)
        public virtual ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();

        // 3. Một User (Engineer) báo cáo nhiều tiến độ (FK: ReporterID trong ProgressReports)
        public virtual ICollection<ProgressReport> ProgressReports { get; set; } = new List<ProgressReport>();

        // 4. Các bảng phục vụ nghiệp vụ Mua sắm vật tư (Purchase Orders)
        public virtual ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new List<PurchaseOrder>();

        // ==========================================
        // AUTH & LOGS SYSTEM (Bổ sung theo ERD & Auth Flow)
        // ==========================================
        
        // Hoạt động hệ thống (Bảng "Activities" dưới đáy sơ đồ ERD)
        public virtual ICollection<ActivityLog> Activities { get; set; } = new List<ActivityLog>();

        // Cảnh báo AI & Báo cáo hệ thống sở hữu bởi User
        public virtual ICollection<AIAlert> AIAlerts { get; set; } = new List<AIAlert>();
        public virtual ICollection<SystemReport> SystemReports { get; set; } = new List<SystemReport>();

        // Token & Bảo mật tài khoản
        public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
        public virtual ICollection<EmailVerification> EmailVerifications { get; set; } = new List<EmailVerification>();
    }

    public enum Role
    {
        ADMIN,
        PM,
        ENGINEER
    }
}
