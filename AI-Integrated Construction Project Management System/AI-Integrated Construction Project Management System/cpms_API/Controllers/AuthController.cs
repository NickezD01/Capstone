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
        public async Task<IActionResult> Register(UserRegisterRequest user)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                return BadRequest(new
                {
                    statusCode = 400,
                    isSuccess = false,
                    errorMessage = string.Join("; ", errors),
                    result = (object?)null
                });
            }
            var result = await _service.RegisterAsync(user);
            return StatusCode((int)result.StatusCode, result);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequest user)
        {
            var result = await _service.LoginAsync(user);
            return StatusCode((int)result.StatusCode, result);
        }

        [HttpPost("Verification")]
        public async Task<IActionResult> Verification(VerificationEmailRequest request)
        {

            var result = await _service.VerifyEmailAsync(request.UserId, request.VerificationCode);
            return StatusCode((int)result.StatusCode, result);
        }

        [HttpPost("resend-verification")]
        public async Task<IActionResult> ResendVerification(ResendVerificationRequest request)
        {
            var result = await _service.ResendVerificationAsync(request.Email);
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

    }
}
