namespace cpms_Application.Request.Tasks;

public sealed class CreateTaskIssueRequest
{
    public string Description { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }
}

public sealed class ResolveTaskIssueRequest
{
    public string? ResolutionNote { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}
