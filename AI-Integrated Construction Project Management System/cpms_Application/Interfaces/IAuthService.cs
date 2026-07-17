using cpms_Application.Request.UserAccount;
using cpms_Application.Response;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Application.Interfaces
{
    public interface IAuthService
    {
        Task<ApiResponse> RegisterAsync(UserRegisterRequest userRequest);
        Task<ApiResponse> LoginAsync(LoginRequest request);
        Task<ApiResponse> VerifyEmailAsync(int userId, string verificationCode);
        Task<ApiResponse> ResendVerificationAsync(string email);
        Task<ApiResponse> RefreshSessionAsync(RefreshSessionRequest request);
        Task<ApiResponse> LogoutAsync(string refreshToken);
        Task<ApiResponse> ForgotPasswordAsync(string email);
        Task<ApiResponse> ResetPasswordAsync(ResetPasswordRequest request);
        Task<ApiResponse> ChangePasswordAsync(ChangePasswordRequest request);
        Task<ApiResponse> AdminResetPasswordAsync(int userId);
    }
}
