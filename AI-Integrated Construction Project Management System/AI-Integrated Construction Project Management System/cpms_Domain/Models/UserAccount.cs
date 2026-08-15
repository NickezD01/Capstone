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
        public string NormalizedEmail { get; private set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public bool? IsEmailVerified { get; set; } = false;
        public int FailedLoginAttempts { get; set; }
        public DateTime? LockoutEnd { get; set; }
        public DateTime PasswordChangedAt { get; set; } = DateTime.UtcNow;
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
        public string? ImgUrl { get; set; }
        public Role Role { get; set; }

        // ==========================================
        // RELATIONSHIPS MAP THEO ĐÚNG ERD
        // ==========================================

        // 1. Một User với vai trò Project Manager có thể quản lý nhiều Dự án (FK: PMUserID trong Projects)
        public virtual ICollection<Project> ManagedProjects { get; set; } = new List<Project>();

        // Project customer view access for the assigned client account
        public virtual ICollection<Project> CustomerProjects { get; set; } = new List<Project>();

        // 2. Một User được giao nhiều Tasks (FK: AssignedToUserID trong Tasks)
        public virtual ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();

        // 3. Một User báo cáo nhiều tiến độ (FK: ReporterID trong ProgressReports)
        public virtual ICollection<ProgressReport> ProgressReports { get; set; } = new List<ProgressReport>();

        // 4. Các bảng phục vụ nghiệp vụ Mua sắm vật tư (Purchase Orders)
        public virtual ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new List<PurchaseOrder>();

        // ==========================================
        // AUTH & LOGS SYSTEM (Bổ sung theo ERD & Auth Flow)
        // ==========================================
        
        // Hoạt động hệ thống (Bảng "Activities" dưới đáy sơ đồ ERD)
        public virtual ICollection<ActivityLog> Activities { get; set; } = new List<ActivityLog>();

        // AI alerts are owned by projects. User ownership is intentionally not modeled.
        public virtual ICollection<SystemReport> SystemReports { get; set; } = new List<SystemReport>();

        // Token & Bảo mật tài khoản
        public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
        public virtual ICollection<EmailVerification> EmailVerifications { get; set; } = new List<EmailVerification>();
    }

    public enum Role
    {
        ADMIN,
        PM,
        WAREHOUSE_MANAGER,
        SUPPLIER,
        CUSTOMER,
        WORKER
    }
}
