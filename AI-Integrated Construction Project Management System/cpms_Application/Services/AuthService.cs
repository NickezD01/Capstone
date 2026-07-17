using cpms_Application.Interfaces;
using cpms_Application.Request.UserAccount;
using cpms_Application.Response;
using cpms_Domain;
using cpms_Domain.Models;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Application.Services
{
    public class AuthService : IAuthService
    {
        private const int PasswordIterations = 210_000;
        private IUnitOfWork _unitOfWork;
        private AppSetting _appSettings;
        private IClaimService _claimService;
        private IEmailService _emailService;
        public AuthService(IUnitOfWork unitOfWork, AppSetting appSettings, IClaimService claimService, IEmailService emailService)
        {
            _unitOfWork = unitOfWork;
            _appSettings = appSettings;
            _claimService = claimService;
            _emailService = emailService;
        }
        public async Task<ApiResponse> RegisterAsync(UserRegisterRequest userRequest)
        {
            ApiResponse response = new ApiResponse();
            var transactionStarted = false;
            try
            {

                var checkPassword = CheckUserPassword(userRequest);
                if (!checkPassword)
                {
                    response.SetBadRequest(message: "Confirm password is wrong !");
                    return response;
                }
                var normalizedEmail = userRequest.Email.Trim().ToLowerInvariant();
                var existingUser = await _unitOfWork.UserAccounts.GetAsync(x => x.Email == normalizedEmail);
                if (existingUser != null)
                {
                    response.SetBadRequest(message: "The email address is already register");
                    return response;
                }
                // Create password hash and save user details
                var pass = CreatePasswordHash(userRequest.Password);
                UserAccount user = new UserAccount()
                {
                    //UserName = userRequest.UserName,
                    PasswordHash = pass.PasswordHash,
                    PasswordSalt = pass.PasswordSalt,
                    Email = normalizedEmail,
                    FirstName = userRequest.FirstName,
                    LastName = userRequest.LastName,
                    Role = Role.CUSTOMER,
                    IsEmailVerified = false // Initially, email is not verified
                };

                await _unitOfWork.BeginTransactionAsync();
                transactionStarted = true;
                await _unitOfWork.UserAccounts.AddAsync(user);
                await _unitOfWork.SaveChangeAsync();

                // Generate verification code
                var verificationCode = GenerateVerificationCode(); // Method to generate the code
                var emailVerification = new EmailVerification
                {
                    UserId = user.Id,
                    VerificationCode = HashVerificationCode(user.Id, verificationCode),
                    ExpiresAt = DateTime.UtcNow.AddMinutes(30), // Code valid for 30 minutes
                    IsUsed = false
                };

                // Save verification code to the database
                await _unitOfWork.EmailVerifications.AddAsync(emailVerification);
                await _unitOfWork.SaveChangeAsync();

                await _unitOfWork.CommitTransactionAsync();
                transactionStarted = false;

                string emailContent = BuildVerificationEmail(user.FirstName, verificationCode);
                var emailResponse = await _emailService.SendValidationEmail(user.Email, emailContent);
                if (!emailResponse.IsSuccess)
                    return response.SetApiResponse(System.Net.HttpStatusCode.ServiceUnavailable, false,
                        "Account created, but the verification email could not be sent. Use the resend-verification endpoint.", user.Id);
                response.SetOk(user.Id);
                return response;
            }
            catch (Exception)
            {
                if (transactionStarted) await _unitOfWork.RollbackTransactionAsync();
                return response.SetApiResponse(System.Net.HttpStatusCode.InternalServerError, false, "Unable to register the account.");
            }

        }
        public async Task<ApiResponse> VerifyEmailAsync(int userId, string verificationCode)
        {
            ApiResponse response = new ApiResponse();
            if (userId <= 0 || string.IsNullOrWhiteSpace(verificationCode))
                return response.SetBadRequest("Invalid or expired verification code.");

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                var hashedCode = HashVerificationCode(userId, verificationCode.Trim());
                var verificationRecord = await _unitOfWork.EmailVerifications.GetAsync(x =>
                    x.UserId == userId && !x.IsUsed &&
                    (x.VerificationCode == hashedCode || x.VerificationCode == verificationCode));
                if (verificationRecord == null || verificationRecord.ExpiresAt < DateTime.UtcNow)
                {
                    if (verificationRecord != null)
                    {
                        verificationRecord.IsUsed = true;
                        await _unitOfWork.SaveChangeAsync();
                    }
                    await _unitOfWork.CommitTransactionAsync();
                    return response.SetBadRequest("Invalid or expired verification code.");
                }

                var user = await _unitOfWork.UserAccounts.GetAsync(x => x.Id == userId);
                if (user == null)
                {
                    await _unitOfWork.RollbackTransactionAsync();
                    return response.SetBadRequest("Invalid or expired verification code.");
                }

                verificationRecord.IsUsed = true;
                user.IsEmailVerified = true;
                await _unitOfWork.SaveChangeAsync();
                await _unitOfWork.CommitTransactionAsync();
                return response.SetOk("Email verified successfully.");
            }
            catch (Exception)
            {
                await _unitOfWork.RollbackTransactionAsync();
                return response.SetApiResponse(System.Net.HttpStatusCode.InternalServerError, false, "Unable to verify the email address.");
            }
        }
        private PasswordDTO CreatePasswordHash(string password)
        {
            var salt = RandomNumberGenerator.GetBytes(32);
            return new PasswordDTO
            {
                PasswordSalt = salt,
                PasswordHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, PasswordIterations,
                    HashAlgorithmName.SHA512, 64)
            };
        }
        public async Task<ApiResponse> LoginAsync(LoginRequest request)
        {
            ApiResponse response = new ApiResponse();
            var normalizedEmail = request.UserEmail.Trim().ToLowerInvariant();
            var account = await _unitOfWork.UserAccounts.GetAsync(u => u.Email == normalizedEmail);
            if (account == null || !VerifyPasswordHash(request.Password, account.PasswordHash, account.PasswordSalt))
            {
                response.SetBadRequest(message: "Email or password is wrong");
                return response;
            }

            if (account.IsEmailVerified == false)
            {
                response.SetBadRequest(message: "Please Verify your email");
                return response;
            }

            if (IsLegacyPasswordHash(account.PasswordSalt))
            {
                var upgraded = CreatePasswordHash(request.Password);
                account.PasswordHash = upgraded.PasswordHash;
                account.PasswordSalt = upgraded.PasswordSalt;
                await _unitOfWork.SaveChangeAsync();
            }

            response.SetOk(CreateToken(account));
            return response;
        }


        private bool VerifyPasswordHash(string password, byte[] passwordHash, byte[] passwordSalt)
        {
            byte[] computedHash;
            if (IsLegacyPasswordHash(passwordSalt))
            {
                using var hmac = new HMACSHA512(passwordSalt);
                computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
            }
            else
            {
                computedHash = Rfc2898DeriveBytes.Pbkdf2(password, passwordSalt, PasswordIterations,
                    HashAlgorithmName.SHA512, 64);
            }
            return computedHash.Length == passwordHash.Length &&
                   CryptographicOperations.FixedTimeEquals(computedHash, passwordHash);
        }

        private static bool IsLegacyPasswordHash(byte[] salt) => salt.Length != 32;


        private string CreateToken(UserAccount user)
        {
            var fullName = user.FirstName + " " + user.LastName;
            List<Claim> claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Role, user.Role.ToString()),
                new Claim("Role", user.Role.ToString()),
                new Claim( "Email" , user.Email!),
                new Claim("UserId", user.Id.ToString()),
                new Claim("FullName", fullName),
                new Claim(ClaimTypes.Name, fullName),
            };

            var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(
                 _appSettings!.SecretToken.Value));

            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha512Signature);

            var token = new JwtSecurityToken(
                issuer: _appSettings.SecretToken.Issuer,
                audience: _appSettings.SecretToken.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(_appSettings.SecretToken.DurationInMinutes),
                signingCredentials: creds);

            var jwt = new JwtSecurityTokenHandler().WriteToken(token);
            return jwt;
        }

        public async Task<ApiResponse> ResendVerificationAsync(string email)
        {
            var response = new ApiResponse();
            if (string.IsNullOrWhiteSpace(email))
                return response.SetBadRequest("Email is required.");

            var normalizedEmail = email.Trim().ToLowerInvariant();
            var user = await _unitOfWork.UserAccounts.GetAsync(x => x.Email == normalizedEmail);
            if (user == null || user.IsEmailVerified == true)
                return response.SetOk("If the account is eligible, a verification email will be sent.");

            var code = GenerateVerificationCode();
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                await _unitOfWork.EmailVerifications.AddAsync(new EmailVerification
                {
                    UserId = user.Id,
                    VerificationCode = HashVerificationCode(user.Id, code),
                    ExpiresAt = DateTime.UtcNow.AddMinutes(30),
                    IsUsed = false
                });
                await _unitOfWork.SaveChangeAsync();
                await _unitOfWork.CommitTransactionAsync();
            }
            catch (Exception)
            {
                await _unitOfWork.RollbackTransactionAsync();
                return response.SetApiResponse(System.Net.HttpStatusCode.InternalServerError, false, "Unable to create a verification code.");
            }

            var emailResponse = await _emailService.SendValidationEmail(user.Email!, BuildVerificationEmail(user.FirstName, code));
            return emailResponse.IsSuccess
                ? response.SetOk("If the account is eligible, a verification email will be sent.")
                : response.SetApiResponse(System.Net.HttpStatusCode.ServiceUnavailable, false, "The verification email could not be sent. Try again later.");
        }

        private string HashVerificationCode(int userId, string code)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_appSettings.SecretToken.Value));
            var hash = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{userId}:{code}")));
            return hash[..20];
        }

        private static string BuildVerificationEmail(string? firstName, string code) =>
            $"Dear {System.Net.WebUtility.HtmlEncode(firstName)},<br/>Please use the following verification code to validate your email: <strong>{code}</strong>.<br/>The code will expire in 30 minutes.";

        private string GenerateVerificationCode()
        {
            return RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        }
        public bool CheckUserPassword(UserRegisterRequest user)
        {
            if (user.Password is null) return (false);
            return (user.Password.Equals(user.ConfirmPassword));
        }

        public class PasswordDTO
        {
            public byte[] PasswordHash { get; set; } = new byte[32];
            public byte[] PasswordSalt { get; set; } = new byte[32];
        }


    }
}
