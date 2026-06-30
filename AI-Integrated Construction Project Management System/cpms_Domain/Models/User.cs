using System;
using System.Collections.Generic;

namespace cpms_Domain.Models;

public partial class User
{
    public long UserId { get; set; }

    public string? FullName { get; set; }

    public string Email { get; set; } = null!;

    public string? Role { get; set; }

    public string? PasswordHash { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime? CreatedAt { get; set; }

    public virtual ICollection<EmailVerification> EmailVerifications { get; set; } = new List<EmailVerification>();

    public virtual ICollection<MaterialRequest> MaterialRequests { get; set; } = new List<MaterialRequest>();

    public virtual ICollection<ProgressReport> ProgressReports { get; set; } = new List<ProgressReport>();

    public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
