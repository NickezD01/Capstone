using System;
using System.Collections.Generic;

namespace cpms_Domain.Models;

public partial class EmailVerification
{
    public long Id { get; set; }

    public string VerificationCode { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }

    public bool IsUsed { get; set; }

    public long UserId { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime? ModifiedDate { get; set; }

    public string? CreatedBy { get; set; }

    public string? ModifiedBy { get; set; }

    public bool IsDeleted { get; set; }

    public virtual User User { get; set; } = null!;
}
