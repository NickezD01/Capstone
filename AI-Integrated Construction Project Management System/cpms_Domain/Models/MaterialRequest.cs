using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Domain.Models
{
    public class MaterialRequest : Base
    {
        public int RequestId { get; set; } // Map với RequestID (PK)
        public int ProjectId { get; set; } // Khóa ngoại trỏ về Projects
        public int? TaskId { get; set; }
        public int? WarehouseId { get; set; }
        public int RequestedBy { get; set; } // Khóa ngoại trỏ về Users (Người tạo yêu cầu)
        public DateTime RequestDate { get; set; }
        public string Status { get; set; } = "PENDING";
        public string? RequestNote { get; set; }
        public int? ApprovedByUserId { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public string? DecisionNote { get; set; }
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        // Navigation Properties
        public virtual Project Project { get; set; } = null!;
        public virtual UserAccount Requester { get; set; } = null!;
        public virtual UserAccount? Approver { get; set; }
        public virtual Warehouse? Warehouse { get; set; }

        // Một phiếu yêu cầu tổng sẽ có nhiều dòng vật tư chi tiết bên dưới
        public virtual TaskItem? TaskItem { get; set; }
        public virtual ICollection<MaterialRequisition> Requisitions { get; set; } = new List<MaterialRequisition>();
        public virtual ICollection<InventoryReservation> Reservations { get; set; } = new List<InventoryReservation>();
    }

    public static class MaterialRequestStatuses
    {
        public const string Pending = "PENDING";
        public const string Approved = "APPROVED";
        public const string PartiallyApproved = "PARTIALLY_APPROVED";
        public const string Rejected = "REJECTED";
        public const string Issued = "ISSUED";
        public const string PartiallyIssued = "PARTIALLY_ISSUED";
        public const string Released = "RELEASED";
        public const string Cancelled = "CANCELLED";
    }
}
