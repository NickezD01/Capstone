namespace cpms_Application.Response.AiConstructionPlanner
{
    /// <summary>
    /// AI Word import draft. Nothing is persisted: the frontend shows the draft
    /// for review, creates the project through the normal endpoint, then sends
    /// <see cref="ProjectAiPlanPreviewResponse"/> to the confirm endpoint.
    /// </summary>
    public class AiImportPreviewResponse
    {
        public AiImportProjectResponse Project { get; set; } = new();
        public ProjectAiPlanPreviewResponse Plan { get; set; } = new();
    }

    public class AiImportProjectResponse
    {
        public string ProjectName { get; set; } = string.Empty;
        public string? Address { get; set; }
        public decimal TotalBudget { get; set; }
        public string Currency { get; set; } = "VND";
        public DateTime StartDate { get; set; }
        public DateTime BaselineStart { get; set; }
        public DateTime BaselineEnd { get; set; }
    }

    /// <summary>Raw extraction contract exchanged with the AI. Property matching
    /// is case-insensitive; the AI is instructed to emit camelCase.</summary>
    public class AiExtractedProjectPlan
    {
        public string? ProjectName { get; set; }
        public string? Address { get; set; }
        public decimal TotalBudget { get; set; }
        public string? Currency { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime BaselineStart { get; set; }
        public DateTime BaselineEnd { get; set; }
        public List<AiExtractedPhase> Phases { get; set; } = new();
        public List<AiExtractedTask> Tasks { get; set; } = new();
    }

    public class AiExtractedPhase
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public int SequenceOrder { get; set; }
        public DateTime BaselineStart { get; set; }
        public DateTime BaselineEnd { get; set; }
    }

    public class AiExtractedTask
    {
        public string? PhaseRef { get; set; }
        public string? TaskName { get; set; }
        public decimal PlannedBudget { get; set; }
        public DateTime BaselineStart { get; set; }
        public DateTime BaselineEnd { get; set; }
    }
}
