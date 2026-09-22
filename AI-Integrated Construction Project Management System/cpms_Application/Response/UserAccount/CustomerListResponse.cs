namespace cpms_Application.Response.UserAccount
{
    /// <summary>
    /// Minimal customer picker entry for PM project assignment.
    /// Verified customers only; no sensitive fields are exposed.
    /// </summary>
    public class CustomerListResponse
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }
}
