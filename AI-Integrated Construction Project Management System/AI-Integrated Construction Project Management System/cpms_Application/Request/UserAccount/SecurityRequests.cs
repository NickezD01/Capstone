namespace cpms_Application.Request.UserAccount;

public sealed class RefreshSessionRequest
{
    public string RefreshToken { get; set; } = string.Empty;
    public string? DeviceInfo { get; set; }
}

public sealed class LogoutRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

public sealed class ForgotPasswordRequest
{
    public string Email { get; set; } = string.Empty;
}

public sealed class ResetPasswordRequest
{
    public int UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}

public sealed class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}

public sealed class AdminResetPasswordRequest
{
    public int UserId { get; set; }
}
