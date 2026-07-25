using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Domain.Models
{
    public class Project : Base
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = null!;
        public string? Address { get; set; }
        public ProjectStatus Status { get; set; } = ProjectStatus.PLANNING;

        // Cân chỉnh các trường ngày tháng chuẩn ERD
        public DateTime StartDate { get; set; }
        public DateTime BaselineStart { get; set; }
        public DateTime BaselineEnd { get; set; }

        // Các trường phục vụ bài toán ngân sách hiện tại của bạn
        public decimal TotalProjectBudget { get; set; }
        public string Currency { get; set; } = "VND";

        // 🚀 BỔ SUNG KHÓA NGOẠI: Người quản lý dự án (Project Manager)
        public int PMUserID { get; set; }
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();


        // ==========================================
        // NAVIGATION PROPERTIES MAP THEO ERD
        // ==========================================

        // 1. Trỏ ngược về ông PM quản lý dự án này
        public virtual UserAccount ProjectManager { get; set; } = null!;

        // 2. Dự án được chia thành nhiều Đầu việc (Tasks assigned to)
        public virtual ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();

        // 3. Dự án tạo các yêu cầu vật tư (Bảng MaterialsRequests trong ERD)
        public virtual ICollection<MaterialRequest> MaterialRequests { get; set; } = new List<MaterialRequest>();

        // 4. Dự án có các đơn mua hàng (Bảng PurchaseOrders / POs)
        public virtual ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new List<PurchaseOrder>();
        public virtual ICollection<ProjectBudgetHistory> BudgetHistories { get; set; } = new List<ProjectBudgetHistory>();

        // 5. Hệ thống cảnh báo AI & Báo cáo thuộc về dự án này (Bổ sung theo ERD)
        public virtual ICollection<AIAlert> AIAlerts { get; set; } = new List<AIAlert>();
        public virtual ICollection<SystemReport> SystemReports { get; set; } = new List<SystemReport>();

        public void Start(DateTime utcNow)
        {
            if (Status is not (ProjectStatus.PLANNING or ProjectStatus.DELAYED))
                throw new InvalidOperationException("Only a planning or delayed project can be started.");
            Status = utcNow > BaselineEnd ? ProjectStatus.DELAYED : ProjectStatus.IN_PROGRESS;
        }

        public void Pause()
        {
            if (Status is not (ProjectStatus.IN_PROGRESS or ProjectStatus.DELAYED))
                throw new InvalidOperationException("Only an active project can be paused.");
            Status = ProjectStatus.PAUSED;
        }

        public void Cancel()
        {
            if (Status == ProjectStatus.COMPLETED) throw new InvalidOperationException("A completed project cannot be cancelled.");
            Status = ProjectStatus.CANCELLED;
        }

        public void Reopen()
        {
            if (Status is not (ProjectStatus.PAUSED or ProjectStatus.CANCELLED))
                throw new InvalidOperationException("Only a paused or cancelled project can be reopened.");
            Status = ProjectStatus.PLANNING;
        }

        public void Complete(bool allRequiredTasksClosed)
        {
            if (Status is not (ProjectStatus.IN_PROGRESS or ProjectStatus.DELAYED))
                throw new InvalidOperationException("Only an active project can be completed.");
            if (!allRequiredTasksClosed)
                throw new InvalidOperationException("Every non-cancelled project task must be completed first.");
            Status = ProjectStatus.COMPLETED;
        }

        public void UpdatePlan(string name, string? address, DateTime startDate, DateTime baselineStart, DateTime baselineEnd)
        {
            if (Status is ProjectStatus.COMPLETED or ProjectStatus.CANCELLED)
                throw new InvalidOperationException("A closed project cannot be edited.");
            if (baselineEnd < baselineStart || startDate < baselineStart || startDate > baselineEnd)
                throw new ArgumentException("Project dates are invalid.");
            ProjectName = name.Trim();
            Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
            StartDate = startDate;
            BaselineStart = baselineStart;
            BaselineEnd = baselineEnd;
        }
    }

    public enum ProjectStatus
    {
        PLANNING,
        IN_PROGRESS,
        COMPLETED,
        DELAYED,
        PAUSED,
        CANCELLED
    }
}
