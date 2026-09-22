using cpms_Application.Interfaces;
using cpms_Application.Request.UserAccount;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace cpms_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        public IAuthService _service;
        public AuthController(IAuthService service)
        {
            _service = service;
        }

        [HttpPost("register")]
        public IActionResult Register(UserRegisterRequest user) =>
            Gone("Self-registration is no longer supported. Accounts are created by an administrator.");

        [HttpPost("Verification")]
        public IActionResult Verification(VerificationEmailRequest request) =>
            Gone("Email verification is retired. Accounts created by an administrator are verified automatically.");

        [HttpPost("resend-verification")]
        public IActionResult ResendVerification(ResendVerificationRequest request) =>
            Gone("Email verification is retired. Accounts created by an administrator are verified automatically.");

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequest user)
        {
            var result = await _service.LoginAsync(user);
            return StatusCode((int)result.StatusCode, result);
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh(RefreshSessionRequest request)
        {
            var result = await _service.RefreshSessionAsync(request);
            return StatusCode((int)result.StatusCode, result);
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout(LogoutRequest request)
        {
            var result = await _service.LogoutAsync(request.RefreshToken);
            return StatusCode((int)result.StatusCode, result);
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request)
        {
            var result = await _service.ForgotPasswordAsync(request.Email);
            return StatusCode((int)result.StatusCode, result);
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
        {
            var result = await _service.ResetPasswordAsync(request);
            return StatusCode((int)result.StatusCode, result);
        }

        [Authorize]
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
        {
            var result = await _service.ChangePasswordAsync(request);
            return StatusCode((int)result.StatusCode, result);
        }

        [Authorize(Roles = "ADMIN")]
        [HttpPost("admin/reset-password/{userId:int}")]
        public async Task<IActionResult> AdminResetPassword(int userId)
        {
            var result = await _service.AdminResetPasswordAsync(userId);
            return StatusCode((int)result.StatusCode, result);
        }

        private ObjectResult Gone(string message) =>
            StatusCode((int)System.Net.HttpStatusCode.Gone,
                new cpms_Application.Response.ApiResponse().SetApiResponse(
                    System.Net.HttpStatusCode.Gone, false, message));
    }
}
