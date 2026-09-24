namespace cpms_Application.Response.Phase;

public sealed class PhaseSummaryResponse
{
    public int PhaseId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int WorkCategoryId { get; set; }
    public string WorkCategoryName { get; set; } = string.Empty;
    public int SequenceOrder { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime BaselineStart { get; set; }
    public DateTime BaselineEnd { get; set; }
}
