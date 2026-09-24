namespace cpms_Application.Response.RiskAssessment
{
    /// <summary>
    /// Deterministic delay/risk scan. Computed on demand, never persisted.
    /// Only the 7 data-backed risk types are produced; dependency risk is
    /// excluded until task dependencies are modeled.
    /// </summary>
    public class ProjectRiskResponse
    {
        public int ProjectId { get; set; }
        public DateTime GeneratedAt { get; set; }
        public List<RiskItemResponse> Risks { get; set; } = new();
    }

    public class RiskItemResponse
    {
        public string RiskType { get; set; } = string.Empty;
        public string Severity { get; set; } = RiskSeverity.Warning;
        public int? TaskId { get; set; }
        public string? TaskName { get; set; }
        public int? VariantId { get; set; }
        public string Message { get; set; } = string.Empty;
        public Dictionary<string, decimal> Metrics { get; set; } = new();
    }

    public static class ProjectRiskTypes
    {
        public const string ScheduleDelay = "SCHEDULE_DELAY";
        public const string WorkItemDelay = "WORK_ITEM_DELAY";
        public const string OverallProjectDelay = "OVERALL_PROJECT_DELAY";
        public const string MaterialShortage = "MATERIAL_SHORTAGE";
        public const string MaterialAvailability = "MATERIAL_AVAILABILITY";
        public const string ProgressDeviation = "PROGRESS_DEVIATION";
        public const string BudgetRisk = "BUDGET_RISK";
    }

    public static class RiskSeverity
    {
        public const string Warning = "WARNING";
        public const string Critical = "CRITICAL";
    }

    /// <summary>
    /// AI-recommended corrective actions for a risk scan. Preview only:
    /// actions are never applied automatically.
    /// </summary>
    public class RecommendRiskActionsResponse
    {
        public int ProjectId { get; set; }
        public DateTime GeneratedAt { get; set; }
        public List<RiskActionItemResponse> Actions { get; set; } = new();
    }

    public class RiskActionItemResponse
    {
        public int Priority { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public string OwnerRole { get; set; } = string.Empty;
        public List<string> RelatedRiskTypes { get; set; } = new();
    }

    public class AiRiskActionPlan
    {
        public List<AiRiskActionRow> Actions { get; set; } = new();
    }

    public class AiRiskActionRow
    {
        public int Priority { get; set; }
        public string? Title { get; set; }
        public string? Detail { get; set; }
        public string? OwnerRole { get; set; }
        public List<string> RelatedRiskTypes { get; set; } = new();
    }
}
