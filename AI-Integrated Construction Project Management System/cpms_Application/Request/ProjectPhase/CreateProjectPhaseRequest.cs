using System;

namespace cpms_Application.Request.ProjectPhase
{
    public class CreateProjectPhaseRequest
    {
        public int ProjectId { get; set; }
        public string PhaseName { get; set; } = null!;
        public string? Description { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }
}
