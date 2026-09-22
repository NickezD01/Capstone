using cpms_Domain.Models;

namespace cpms_Application.Request.User
{
    /// <summary>
    /// ADMIN-only provisioning. The account is created verified with the given
    /// role and admin-set password. SUPPLIER cannot be assigned (retired role).
    /// </summary>
    public class CreateUserAccountRequest
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public Role Role { get; set; }
        public string Password { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
