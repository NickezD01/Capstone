namespace cpms_Application.Request.Tasks;

public sealed class UpdateTaskRequest
{
    public int ProjectPhaseId { get; set; }
    public string TaskName { get; set; } = string.Empty;
    public int AssignedToUserID { get; set; }
    public decimal PlannedBudget { get; set; }
    public DateTime BaselineStart { get; set; }
    public DateTime BaselineEnd { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class TaskLifecycleRequest
{
    public string RowVersion { get; set; } = string.Empty;
}
