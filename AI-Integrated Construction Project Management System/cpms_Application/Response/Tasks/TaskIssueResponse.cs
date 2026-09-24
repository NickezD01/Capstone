namespace cpms_Application.Response.Tasks;

public sealed class TaskIssueResponse
{
    public int IssueId { get; set; }
    public int TaskId { get; set; }
    public int ReportedByUserId { get; set; }
    public string ReportedByName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ResolutionNote { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}
