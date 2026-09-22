using cpms_Application.Response.AiConstructionPlanner;

namespace cpms_Application.Request.AiConstructionPlanner
{
    /// <summary>
    /// Step 12 AI preview/confirm workflow. Previews are stateless: generate endpoints
    /// return proposals with temporary IDs, and the PM echoes the (possibly edited)
    /// proposal back to the confirm endpoint. Nothing is persisted until confirm.
    /// </summary>
    /// <summary>
    /// Frontend-owned structured project brief (7 fields). The frontend hardcodes
    /// its own question form and sends this brief; the backend maps it onto the
    /// same planner prompt contract as the legacy five answers.
    /// When both <c>Brief</c> and <c>Answers</c> are supplied, the brief wins.
    /// </summary>
    public class AiProjectBriefRequest
    {
        public string ProjectType { get; set; } = string.Empty;
        public decimal FloorAreaM2 { get; set; }
        public int NumberOfFloors { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal? Budget { get; set; }
        public string? SpecialRequirements { get; set; }
    }

    public class GenerateProjectAiPhasesRequest
    {
        public ConstructionPlanAnswersRequest? Answers { get; set; }
        public AiProjectBriefRequest? Brief { get; set; }
    }

    public class GenerateProjectAiTasksRequest
    {
        public ConstructionPlanAnswersRequest? Answers { get; set; }
        public AiProjectBriefRequest? Brief { get; set; }

        /// <summary>
        /// Generate tasks for one existing phase. The phase must belong to the project.
        /// Exactly one of <see cref="PhaseId"/> or <see cref="Phases"/> must be supplied.
        /// </summary>
        public int? PhaseId { get; set; }

        /// <summary>
        /// Echo of the phase preview from phases:generate. Generated tasks are mapped
        /// to these proposed phases by the stable <c>AiKey</c>, so the PM may rename
        /// phases without breaking the mapping.
        /// </summary>
        public List<AiPhaseProposalResponse>? Phases { get; set; }
    }

    /// <summary>
    /// "AI finish the planning for me": the backend reads the current phases
    /// and tasks, combines them with the brief (or legacy answers) plus an
    /// optional focus note, and previews only the remaining work.
    /// When both <c>Brief</c> and <c>Answers</c> are supplied, the brief wins.
    /// </summary>
    public class CompleteProjectAiPlanRequest
    {
        public ConstructionPlanAnswersRequest? Answers { get; set; }
        public AiProjectBriefRequest? Brief { get; set; }
        public string? FocusNote { get; set; }
    }

    public class ConfirmProjectAiPlanRequest
    {
        /// <summary>
        /// Final (PM-edited) phase proposals. Every entry must carry a non-empty
        /// temporary ID that tasks can reference via <c>PhaseTempId</c>.
        /// May be empty only when every task references an existing phase.
        /// </summary>
        public List<AiPhaseProposalRequest> Phases { get; set; } = new();

        public List<AiTaskProposalRequest> Tasks { get; set; } = new();
    }

    public class AiPhaseProposalRequest
    {
        public string TempId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int SequenceOrder { get; set; }
        public DateTime BaselineStart { get; set; }
        public DateTime BaselineEnd { get; set; }
    }

    public class AiTaskProposalRequest
    {
        public string TempId { get; set; } = string.Empty;

        /// <summary>
        /// Temporary ID of a phase in the same request. Exactly one of
        /// <see cref="PhaseTempId"/> or <see cref="PhaseId"/> must be set.
        /// </summary>
        public string? PhaseTempId { get; set; }

        /// <summary>Existing phase the task belongs to. Must belong to the project.</summary>
        public int? PhaseId { get; set; }

        public string TaskName { get; set; } = string.Empty;
        public DateTime BaselineStart { get; set; }
        public DateTime BaselineEnd { get; set; }
        public decimal PlannedBudget { get; set; }
    }
}
