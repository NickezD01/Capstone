using cpms_Application.Response.AiConstructionPlanner;

namespace cpms_Application.Request.AiConstructionPlanner
{
    public class GenerateConstructionPlanRequest
    {
        public int? ProjectId { get; set; }
        public ConstructionPlanAnswersRequest? Answers { get; set; }
    }

    public class GenerateConstructionPlanExcelRequest
    {
        public ConstructionPlanJsonResponse? Plan { get; set; }
        public string? FileName { get; set; }
    }

    public class ConstructionPlanAnswersRequest
    {
        public string? ProjectOverview { get; set; }
        public string? LocationAndSite { get; set; }
        public string? Timeline { get; set; }
        public string? BudgetAndQuality { get; set; }
        public string? SpecialRequirements { get; set; }
    }
}
