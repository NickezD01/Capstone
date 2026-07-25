namespace cpms_Application.Request.ProgressReport;

public sealed class ReviewProgressReportRequest
{
    public string? ReviewNote { get; set; }
    public bool AllowCostOverrun { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class CorrectProgressReportRequest
{
    public decimal ProgressIncrement { get; set; }
    public decimal ActualCostIncrement { get; set; }
    public string? Notes { get; set; }
    public string? SitePhotoUrl { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}
