namespace cpms_Application.Request.Phase;

public sealed class CreatePhaseRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SequenceOrder { get; set; }
    public DateTime BaselineStart { get; set; }
    public DateTime BaselineEnd { get; set; }
}
