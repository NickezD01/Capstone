namespace cpms_Application.Response.UserAccount
{
    /// <summary>
    /// Minimal site-worker picker entry for PM task assignment.
    /// Verified workers only; no sensitive fields are exposed.
    /// </summary>
    public class WorkerListResponse
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }
}
