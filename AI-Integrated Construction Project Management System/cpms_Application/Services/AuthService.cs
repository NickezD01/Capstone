using cpms_Application.Authorization;
using cpms_Application.Interfaces;
using cpms_Application.Request.UserAccount;
using cpms_Application.Response;
using cpms_Application.Security;
using cpms_Domain;
using cpms_Domain.Models;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace cpms_Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly AppSetting _appSettings;
        private readonly IEmailService _emailService;

        public AuthService(IUnitOfWork unitOfWork, AppSetting appSettings, IClaimService claimService, IEmailService emailService)
        {
            _unitOfWork = unitOfWork;
            _appSettings = appSettings;
            _emailService = emailService;
        }

        public async Task<ApiResponse> RegisterAsync(UserRegisterRequest userRequest)
        {
            var response = new ApiResponse();
            try
            {
                if (!CheckUserPassword(userRequest))
                {
                    return response.SetBadRequest("Confirm password is wrong!");
                }

                var email = userRequest.Email.Trim();
                var existingUser = await _unitOfWork.Users.GetAsync(x => x.Email == email);
                if (existingUser != null)
                {
                    return response.SetBadRequest("The email address is already registered.");
                }

                var userCount = await _unitOfWork.Users.CountAsync();
                var role = userCount == 0 ? AppRoles.Admin : AppRoles.Customer;

                var user = new User
                {
                    Email = email,
                    FullName = BuildFullName(userRequest.FirstName, userRequest.LastName),
                    Role = role,
                    PasswordHash = PasswordHasher.HashPassword(userRequest.Password),
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                await _unitOfWork.Users.AddAsync(user);
                await _unitOfWork.SaveChangeAsync();

                var verificationCode = GenerateVerificationCode();
                var emailVerification = new EmailVerification
                {
                    UserId = user.UserId,
                    VerificationCode = verificationCode,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(30),
                    IsUsed = false,
                    CreatedDate = DateTime.UtcNow,
                    IsDeleted = false
                };

                await _unitOfWork.EmailVerifications.AddAsync(emailVerification);
                await _unitOfWork.SaveChangeAsync();

                var emailContent = $"Dear {user.FullName},<br/>Please use the following verification code to validate your email: <strong>{verificationCode}</strong>.<br/>The code will expire in 30 minutes.";
                var emailResponse = await _emailService.SendValidationEmail(user.Email, emailContent);
                if (!emailResponse.IsSuccess)
                {
                    return response.SetBadRequest("Failed to send verification email.");
                }

                return response.SetOk(new { user.UserId, user.Email, user.FullName, user.Role });
            }
            catch (Exception ex)
            {
                return response.SetBadRequest($"Error: {ex.Message}. Details: {ex.InnerException?.Message}");
            }
        }

        public async Task<ApiResponse> VerifyEmailAsync(long userId, string verificationCode)
        {
            var response = new ApiResponse();

            var verificationRecord = await _unitOfWork.EmailVerifications
                .GetAsync(x => x.UserId == userId && x.VerificationCode == verificationCode && x.IsUsed == false && x.IsDeleted == false);

            if (verificationRecord == null)
            {
                return response.SetBadRequest("Invalid or expired verification code.");
            }

            if (verificationRecord.ExpiresAt < DateTime.UtcNow)
            {
                return response.SetBadRequest("The verification code has expired.");
            }

            verificationRecord.IsUsed = true;
            verificationRecord.ModifiedDate = DateTime.UtcNow;
            await _unitOfWork.SaveChangeAsync();

            return response.SetOk("Email verified successfully.");
        }

        public async Task<ApiResponse> LoginAsync(LoginRequest request)
        {
            var response = new ApiResponse();
            var email = request.UserEmail.Trim();
            var account = await _unitOfWork.Users.GetAsync(u => u.Email == email);
            if (account == null || string.IsNullOrWhiteSpace(account.PasswordHash) || !PasswordHasher.VerifyPassword(request.Password, account.PasswordHash))
            {
                return response.SetBadRequest("Email or password is wrong.");
            }

            if (!account.IsActive)
            {
                return response.SetBadRequest("This account is disabled.");
            }

            var verifications = await _unitOfWork.EmailVerifications.GetAllAsync(v => v.UserId == account.UserId && v.IsDeleted == false);
            if (verifications.Any() && !verifications.Any(v => v.IsUsed))
            {
                return response.SetBadRequest("Please verify your email.");
            }

            return response.SetOk(CreateToken(account));
        }

        private static string BuildFullName(string firstName, string lastName)
        {
            return string.Join(" ", new[] { firstName, lastName }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();
        }

        private string CreateToken(User user)
        {
            var role = AppRoles.Normalize(user.Role);
            var fullName = user.FullName ?? string.Empty;
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Role, role),
                new Claim("Role", role),
                new Claim("Email", user.Email),
                new Claim("UserId", user.UserId.ToString()),
                new Claim("FullName", fullName),
                new Claim(ClaimTypes.Name, fullName)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_appSettings.SecretToken.Value));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha512Signature);
            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.UtcNow.AddDays(1),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private static string GenerateVerificationCode()
        {
            return System.Security.Cryptography.RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        }

        public bool CheckUserPassword(UserRegisterRequest user)
        {
            return !string.IsNullOrWhiteSpace(user.Password) && user.Password.Equals(user.ConfirmPassword);
        }
    }
}
