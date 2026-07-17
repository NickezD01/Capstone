using cpms_Application.Interfaces;
using cpms_Application.Request.UserAccount;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;

namespace cpms_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [EnableRateLimiting("auth")]
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
    }
}
