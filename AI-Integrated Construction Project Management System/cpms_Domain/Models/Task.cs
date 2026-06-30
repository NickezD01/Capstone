using System;
using System.Collections.Generic;

namespace cpms_Domain.Models;

public partial class Task
{
    public long TaskId { get; set; }

    public long ProjectId { get; set; }

    public string? PhaseName { get; set; }

    public string? TaskName { get; set; }

    public DateOnly? BaselineStart { get; set; }

    public DateOnly? BaselineEnd { get; set; }

    public int? ActualProgressPct { get; set; }

    public string? Status { get; set; }

    public virtual ICollection<ProgressReport> ProgressReports { get; set; } = new List<ProgressReport>();

    public virtual Project Project { get; set; } = null!;
}
