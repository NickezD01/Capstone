namespace cpms_Application.Request.Project;

public sealed class UpdateProjectRequest
{
    public string ProjectName { get; set; } = string.Empty;
    public string? Address { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime BaselineStart { get; set; }
    public DateTime BaselineEnd { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class ProjectLifecycleRequest
{
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class ReassignProjectManagerRequest
{
    public int ProjectManagerUserId { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class ReassignProjectCustomerRequest
{
    public int CustomerUserId { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}
