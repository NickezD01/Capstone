using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Domain.Models
{
    public partial class RefreshToken : Base
    {
        public int TokenId { get; set; }

        public int UserId { get; set; }

        public string Token { get; set; } = string.Empty;

        public DateTime ExpiresAt { get; set; }

        public bool IsRevoked { get; set; }
        public DateTime? RevokedAt { get; set; }
        public string? ReplacedByTokenHash { get; set; }
        public Guid SessionFamilyId { get; set; }
        public string? ParentTokenHash { get; set; }
        public DateTime? ReuseDetectedAt { get; set; }
        public string? DeviceInfo { get; set; }

        public DateTime CreatedAt { get; set; }

        public virtual UserAccount User { get; set; } = null!;

        public bool IsActive(DateTime utcNow) => !IsRevoked && ExpiresAt > utcNow;
    }
}
