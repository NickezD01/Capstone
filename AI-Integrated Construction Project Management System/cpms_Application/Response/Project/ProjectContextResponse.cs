namespace cpms_Application.Response.Project
{
    /// <summary>
    /// Minimal project header for site workers: name, address, and dates only.
    /// </summary>
    public sealed class ProjectContextResponse
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public string? Address { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime BaselineStart { get; set; }
        public DateTime BaselineEnd { get; set; }
    }
}
