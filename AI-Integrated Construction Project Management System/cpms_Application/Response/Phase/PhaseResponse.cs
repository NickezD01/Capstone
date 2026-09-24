namespace cpms_Application.Response.Phase;

public sealed class PhaseResponse
{
    public int PhaseId { get; set; }
    public int ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SequenceOrder { get; set; }
    public DateTime BaselineStart { get; set; }
    public DateTime BaselineEnd { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? CreatedDate { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}
