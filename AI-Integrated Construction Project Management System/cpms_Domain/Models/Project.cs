using System;
using System.Collections.Generic;

namespace cpms_Domain.Models;

public partial class Project
{
    public long ProjectId { get; set; }

    public string? ProjectName { get; set; }

    public string? Address { get; set; }

    public string? Status { get; set; }

    public long? ProjectManagerId { get; set; }

    public long? CustomerId { get; set; }

    public DateOnly? BaselineStart { get; set; }

    public DateOnly? BaselineEnd { get; set; }

    public DateTime? CreatedDate { get; set; }

    public virtual ICollection<MaterialIssue> MaterialIssues { get; set; } = new List<MaterialIssue>();

    public virtual ICollection<MaterialRequest> MaterialRequests { get; set; } = new List<MaterialRequest>();

    public virtual ICollection<Task> Tasks { get; set; } = new List<Task>();
}
