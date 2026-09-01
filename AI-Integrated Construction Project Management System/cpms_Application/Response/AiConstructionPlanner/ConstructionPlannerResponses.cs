namespace cpms_Application.Response.AiConstructionPlanner
{
    public class ConstructionPlannerQuestionsResponse
    {
        public string Version { get; set; } = "1.0";
        public List<ConstructionPlannerQuestionResponse> Questions { get; set; } = new();
    }

    public class ConstructionPlannerQuestionResponse
    {
        public int Order { get; set; }
        public string Field { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public bool Required { get; set; }
        public string Placeholder { get; set; } = string.Empty;
    }

    public class ConstructionPlanJsonResponse
    {
        public string PlanId { get; set; } = string.Empty;
        public string Version { get; set; } = "1.0";
        public DateTime GeneratedAt { get; set; }
        public ConstructionProjectSummaryResponse ProjectSummary { get; set; } = new();
        public ConstructionPlanExcelSheetsResponse ExcelSheets { get; set; } = new();
    }

    public class ConstructionPlanExcelFileResponse
    {
        public byte[] Content { get; set; } = Array.Empty<byte>();
        public string ContentType { get; set; } = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        public string FileName { get; set; } = string.Empty;
    }

    public class ConstructionProjectSummaryResponse
    {
        public string ProjectName { get; set; } = string.Empty;
        public string ProjectType { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Scope { get; set; } = string.Empty;
        public List<string> Assumptions { get; set; } = new();
        public string Currency { get; set; } = string.Empty;
        public decimal EstimatedBudget { get; set; }
        public string? TargetStartDate { get; set; }
        public string? TargetEndDate { get; set; }
        public int EstimatedDurationDays { get; set; }
    }

    public class ConstructionPlanExcelSheetsResponse
    {
        public List<ConstructionOverviewRowResponse> Overview { get; set; } = new();
        public List<ConstructionPhaseRowResponse> Phases { get; set; } = new();
        public List<ConstructionTaskRowResponse> Tasks { get; set; } = new();
        public List<ConstructionMaterialRowResponse> Materials { get; set; } = new();
        public List<ConstructionLaborRowResponse> Labor { get; set; } = new();
        public List<ConstructionEquipmentRowResponse> Equipment { get; set; } = new();
        public List<ConstructionCostPlanRowResponse> CostPlan { get; set; } = new();
        public List<ConstructionProcurementPlanRowResponse> ProcurementPlan { get; set; } = new();
        public List<ConstructionRiskRegisterRowResponse> RiskRegister { get; set; } = new();
        public List<ConstructionPermitChecklistRowResponse> PermitChecklist { get; set; } = new();
        public List<ConstructionSafetyPlanRowResponse> SafetyPlan { get; set; } = new();
        public List<ConstructionMilestoneRowResponse> Milestones { get; set; } = new();
    }

    public class ConstructionOverviewRowResponse
    {
        public string Section { get; set; } = string.Empty;
        public string Item { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
    }

    public class ConstructionPhaseRowResponse
    {
        public string PhaseId { get; set; } = string.Empty;
        public string PhaseName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int StartWeek { get; set; }
        public int EndWeek { get; set; }
        public int DurationDays { get; set; }
        public decimal EstimatedCost { get; set; }
        public List<string> Dependencies { get; set; } = new();
        public List<string> Deliverables { get; set; } = new();
    }

    public class ConstructionTaskRowResponse
    {
        public string TaskId { get; set; } = string.Empty;
        public string PhaseId { get; set; } = string.Empty;
        public string TaskName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int StartWeek { get; set; }
        public int EndWeek { get; set; }
        public int DurationDays { get; set; }
        public List<string> PredecessorTaskIds { get; set; } = new();
        public string ResponsibleRole { get; set; } = string.Empty;
        public decimal EstimatedCost { get; set; }
        public string Priority { get; set; } = string.Empty;
        public string AcceptanceCriteria { get; set; } = string.Empty;
    }

    public class ConstructionMaterialRowResponse
    {
        public string MaterialId { get; set; } = string.Empty;
        public string PhaseId { get; set; } = string.Empty;
        public string MaterialName { get; set; } = string.Empty;
        public string Specification { get; set; } = string.Empty;
        public decimal EstimatedQuantity { get; set; }
        public string Unit { get; set; } = string.Empty;
        public decimal UnitCost { get; set; }
        public decimal TotalCost { get; set; }
        public int NeededByWeek { get; set; }
        public string Notes { get; set; } = string.Empty;
    }

    public class ConstructionLaborRowResponse
    {
        public string LaborId { get; set; } = string.Empty;
        public string PhaseId { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public int EstimatedHeadcount { get; set; }
        public int DurationDays { get; set; }
        public decimal DailyRate { get; set; }
        public decimal TotalCost { get; set; }
        public string Notes { get; set; } = string.Empty;
    }

    public class ConstructionEquipmentRowResponse
    {
        public string EquipmentId { get; set; } = string.Empty;
        public string PhaseId { get; set; } = string.Empty;
        public string EquipmentName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public int DurationDays { get; set; }
        public decimal DailyRate { get; set; }
        public decimal TotalCost { get; set; }
        public string Notes { get; set; } = string.Empty;
    }

    public class ConstructionCostPlanRowResponse
    {
        public string CostCode { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal EstimatedAmount { get; set; }
        public decimal PercentageOfBudget { get; set; }
        public decimal ContingencyAmount { get; set; }
        public string Notes { get; set; } = string.Empty;
    }

    public class ConstructionProcurementPlanRowResponse
    {
        public string ProcurementId { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string SourceType { get; set; } = string.Empty;
        public int RequiredByWeek { get; set; }
        public int LeadTimeDays { get; set; }
        public int OrderByWeek { get; set; }
        public decimal EstimatedCost { get; set; }
        public string RiskLevel { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
    }

    public class ConstructionRiskRegisterRowResponse
    {
        public string RiskId { get; set; } = string.Empty;
        public string RiskCategory { get; set; } = string.Empty;
        public string RiskDescription { get; set; } = string.Empty;
        public string Probability { get; set; } = string.Empty;
        public string Impact { get; set; } = string.Empty;
        public string MitigationPlan { get; set; } = string.Empty;
        public string OwnerRole { get; set; } = string.Empty;
    }

    public class ConstructionPermitChecklistRowResponse
    {
        public string PermitId { get; set; } = string.Empty;
        public string PermitName { get; set; } = string.Empty;
        public bool Required { get; set; }
        public int TargetSubmissionWeek { get; set; }
        public int TargetApprovalWeek { get; set; }
        public string ResponsibleRole { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
    }

    public class ConstructionSafetyPlanRowResponse
    {
        public string SafetyId { get; set; } = string.Empty;
        public string Activity { get; set; } = string.Empty;
        public string Hazard { get; set; } = string.Empty;
        public string ControlMeasure { get; set; } = string.Empty;
        public string InspectionFrequency { get; set; } = string.Empty;
        public string ResponsibleRole { get; set; } = string.Empty;
    }

    public class ConstructionMilestoneRowResponse
    {
        public string MilestoneId { get; set; } = string.Empty;
        public string MilestoneName { get; set; } = string.Empty;
        public int TargetWeek { get; set; }
        public string RelatedPhaseId { get; set; } = string.Empty;
        public string CompletionCriteria { get; set; } = string.Empty;
    }
}
