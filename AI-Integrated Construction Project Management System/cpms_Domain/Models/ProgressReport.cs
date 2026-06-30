using System;
using System.Collections.Generic;

namespace cpms_Domain.Models;

public partial class ProgressReport
{
    public long ReportId { get; set; }

    public long? TaskId { get; set; }

    public long? UserId { get; set; }

    public DateTime? ReportDate { get; set; }

    public int? ProgressIncrement { get; set; }

    public string? Notes { get; set; }

    public string? SitePhotoUrl { get; set; }

    public virtual Task? Task { get; set; }

    public virtual User? User { get; set; }
}
