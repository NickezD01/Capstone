using cpms_Application.Interfaces;
using cpms_Application.Request.UserAccount;
using cpms_Application.Response;
using cpms_Application.Response.UserAccount;
using cpms_Domain;
using cpms_Domain.Models;
using cpms_Application.Security;
using Microsoft.IdentityModel.Tokens;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace cpms_Application.Services;

public sealed class AuthService : IAuthService
{
    private const int PasswordIterations = 210_000;
    private const int MaximumFailedLogins = 5;
    private const int MaximumFailedCodes = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);
    private static readonly TimeSpan EmailVerificationCodeLifetime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan PasswordResetCodeLifetime = TimeSpan.FromMinutes(30);

    private readonly IUnitOfWork _unitOfWork;
    private readonly AppSetting _appSettings;
    private readonly IClaimService _claimService;
    private readonly IEmailService _emailService;

    public AuthService(IUnitOfWork unitOfWork, AppSetting appSettings, IClaimService claimService, IEmailService emailService)
    {
        _unitOfWork = unitOfWork;
        _appSettings = appSettings;
        _claimService = claimService;
        _emailService = emailService;
    }

    public async Task<ApiResponse> RegisterAsync(UserRegisterRequest request)
    {
        if (request.Password != request.ConfirmPassword || !IsStrongPassword(request.Password))
            return BadRequest("The password does not satisfy the security policy.");

        var normalizedEmail = NormalizeEmail(request.Email);
        var existing = await FindByNormalizedEmailAsync(normalizedEmail);
        if (existing != null)
            return new ApiResponse().SetOk("If the address is eligible, verification instructions will be sent.");

        var password = CreatePasswordHash(request.Password);
        var user = new UserAccount
        {
            PasswordHash = password.PasswordHash,
            PasswordSalt = password.PasswordSalt,
            Email = request.Email.Trim().ToLowerInvariant(),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Role = Role.CUSTOMER,
            IsEmailVerified = false,
            PasswordChangedAt = DateTime.UtcNow
        };
        var code = GenerateSecurityCode();

        await _unitOfWork.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        try
        {
            await _unitOfWork.UserAccounts.AddAsync(user);
            await _unitOfWork.SaveChangeAsync();
            await _unitOfWork.EmailVerifications.AddAsync(CreateSecurityToken(user.Id, code, SecurityTokenPurposes.EmailVerification));
            await QueueEmailAsync(user.Email!, BuildVerificationEmail(user.FirstName, code));
            await _unitOfWork.SaveChangeAsync();
            await _unitOfWork.CommitTransactionAsync();
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }

        return new ApiResponse().SetApiResponse(HttpStatusCode.Accepted, true,
            "Account created and verification email queued.", user.Id);
    }

    public async Task<ApiResponse> VerifyEmailAsync(int userId, string verificationCode)
    {
        var result = await ValidateSecurityCodeAsync(userId, verificationCode, SecurityTokenPurposes.EmailVerification);
        if (!result.IsValid) return result.Response;

        var user = await _unitOfWork.UserAccounts.GetByIdAsync(userId);
        if (user == null) return BadRequest("Invalid or expired verification code.");

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            result.Record!.IsUsed = true;
            user.IsEmailVerified = true;
            await _unitOfWork.SaveChangeAsync();
            await _unitOfWork.CommitTransactionAsync();
            return new ApiResponse().SetOk("Email verified successfully.");
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<ApiResponse> ResendVerificationAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return BadRequest("Email is required.");
        var user = await FindByNormalizedEmailAsync(NormalizeEmail(email));
        if (user == null || user.IsEmailVerified == true)
            return new ApiResponse().SetOk("If the address is eligible, verification instructions will be sent.");

        var code = GenerateSecurityCode();
        await ReplaceSecurityTokensAsync(user.Id, SecurityTokenPurposes.EmailVerification, code);
        await QueueEmailAsync(user.Email!, BuildVerificationEmail(user.FirstName, code));
        await _unitOfWork.SaveChangeAsync();
        return new ApiResponse().SetOk("If the address is eligible, verification instructions will be sent.");
    }

    public async Task<ApiResponse> LoginAsync(LoginRequest request)
    {
        var account = await FindByNormalizedEmailAsync(NormalizeEmail(request.UserEmail));
        if (account == null) return Unauthorized();

        var now = DateTime.UtcNow;
        if (account.LockoutEnd.HasValue && account.LockoutEnd > now) return Unauthorized();
        if (!VerifyPasswordHash(request.Password, account.PasswordHash, account.PasswordSalt))
        {
            account.FailedLoginAttempts++;
            if (account.FailedLoginAttempts >= MaximumFailedLogins)
            {
                account.FailedLoginAttempts = 0;
                account.LockoutEnd = now.Add(LockoutDuration);
            }
            await _unitOfWork.SaveChangeAsync();
            return Unauthorized();
        }

        if (account.IsEmailVerified != true)
            return new ApiResponse().SetApiResponse(HttpStatusCode.Forbidden, false, "Email verification is required.");

        account.FailedLoginAttempts = 0;
        account.LockoutEnd = null;
        if (IsLegacyPasswordHash(account.PasswordSalt))
        {
            var upgraded = CreatePasswordHash(request.Password);
            account.PasswordHash = upgraded.PasswordHash;
            account.PasswordSalt = upgraded.PasswordSalt;
            account.PasswordChangedAt = now;
        }

        return new ApiResponse().SetOk(await IssueSessionAsync(account));
    }

    public async Task<ApiResponse> RefreshSessionAsync(RefreshSessionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken)) return Unauthorized();
        var hash = HashOpaqueToken(request.RefreshToken);

        await _unitOfWork.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        try
        {
            var current = await _unitOfWork.RefreshTokens.GetAsync(x => x.Token == hash);
            if (current == null)
            {
                await _unitOfWork.RollbackTransactionAsync();
                return Unauthorized();
            }
            if (current.IsRevoked && current.ReplacedByTokenHash != null)
            {
                var family = await _unitOfWork.RefreshTokens.GetAllAsync(x =>
                    x.UserId == current.UserId && x.SessionFamilyId == current.SessionFamilyId);
                var detectedAt = DateTime.UtcNow;
                foreach (var member in family)
                {
                    member.IsRevoked = true;
                    member.RevokedAt ??= detectedAt;
                    member.ReuseDetectedAt = detectedAt;
                    _unitOfWork.RefreshTokens.Update(member);
                }
                await _unitOfWork.SaveChangeAsync();
                await _unitOfWork.CommitTransactionAsync();
                return Unauthorized();
            }
            if (!current.IsActive(DateTime.UtcNow))
            {
                await _unitOfWork.RollbackTransactionAsync();
                return Unauthorized();
            }
            var user = await _unitOfWork.UserAccounts.GetByIdAsync(current.UserId);
            if (user == null || user.IsEmailVerified != true)
            {
                current.IsRevoked = true;
                current.RevokedAt = DateTime.UtcNow;
                await _unitOfWork.SaveChangeAsync();
                await _unitOfWork.CommitTransactionAsync();
                return Unauthorized();
            }

            var rawReplacement = GenerateOpaqueToken();
            var replacementHash = HashOpaqueToken(rawReplacement);
            current.IsRevoked = true;
            current.RevokedAt = DateTime.UtcNow;
            current.ReplacedByTokenHash = replacementHash;
            var expires = DateTime.UtcNow.Add(RefreshTokenLifetime);
            await _unitOfWork.RefreshTokens.AddAsync(new RefreshToken
            {
                UserId = user.Id,
                Token = replacementHash,
                ParentTokenHash = current.Token,
                SessionFamilyId = current.SessionFamilyId,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = expires,
                DeviceInfo = SanitizeDeviceInfo(request.DeviceInfo)
            });
            await _unitOfWork.SaveChangeAsync();
            await _unitOfWork.CommitTransactionAsync();
            return new ApiResponse().SetOk(CreateSessionResponse(user, rawReplacement, expires));
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<ApiResponse> LogoutAsync(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return new ApiResponse().SetOk();
        await _unitOfWork.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        try
        {
            var token = await _unitOfWork.RefreshTokens.GetAsync(x => x.Token == HashOpaqueToken(refreshToken));
            if (token != null && !token.IsRevoked)
            {
                token.IsRevoked = true;
                token.RevokedAt = DateTime.UtcNow;
                await _unitOfWork.SaveChangeAsync();
            }
            await _unitOfWork.CommitTransactionAsync();
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
        return new ApiResponse().SetOk("Session revoked.");
    }

    public async Task<ApiResponse> ForgotPasswordAsync(string email)
    {
        var generic = new ApiResponse().SetOk("If the address is eligible, password reset instructions will be sent.");
        if (string.IsNullOrWhiteSpace(email)) return generic;
        var user = await FindByNormalizedEmailAsync(NormalizeEmail(email));
        if (user == null || user.IsEmailVerified != true) return generic;

        var code = GenerateSecurityCode();
        await ReplaceSecurityTokensAsync(user.Id, SecurityTokenPurposes.PasswordReset, code);
        await QueueEmailAsync(user.Email!, BuildPasswordResetEmail(user.FirstName, user.Id, code));
        await _unitOfWork.SaveChangeAsync();
        return generic;
    }

    public async Task<ApiResponse> ResetPasswordAsync(ResetPasswordRequest request)
    {
        if (request.NewPassword != request.ConfirmPassword || !IsStrongPassword(request.NewPassword))
            return BadRequest("The password does not satisfy the security policy.");
        var validation = await ValidateSecurityCodeAsync(request.UserId, request.Token, SecurityTokenPurposes.PasswordReset);
        if (!validation.IsValid) return validation.Response;
        var user = await _unitOfWork.UserAccounts.GetByIdAsync(request.UserId);
        if (user == null) return BadRequest("Invalid or expired reset token.");
        return await SetPasswordAsync(user, request.NewPassword, validation.Record);
    }

    public async Task<ApiResponse> ChangePasswordAsync(ChangePasswordRequest request)
    {
        if (request.NewPassword != request.ConfirmPassword || !IsStrongPassword(request.NewPassword))
            return BadRequest("The password does not satisfy the security policy.");
        var user = await _unitOfWork.UserAccounts.GetByIdAsync(_claimService.GetUserClaim().Id);
        if (user == null || !VerifyPasswordHash(request.CurrentPassword, user.PasswordHash, user.PasswordSalt))
            return Unauthorized();
        return await SetPasswordAsync(user, request.NewPassword, null);
    }

    public async Task<ApiResponse> AdminResetPasswordAsync(int userId)
    {
        var claim = _claimService.GetUserClaim();
        if (!string.Equals(claim.Role, Role.ADMIN.ToString(), StringComparison.OrdinalIgnoreCase))
            return new ApiResponse().SetApiResponse(HttpStatusCode.Forbidden, false, "Administrator access is required.");
        var user = await _unitOfWork.UserAccounts.GetByIdAsync(userId);
        if (user == null) return new ApiResponse().SetNotFound("User not found.");
        var code = GenerateSecurityCode();
        await ReplaceSecurityTokensAsync(user.Id, SecurityTokenPurposes.PasswordReset, code);
        await QueueEmailAsync(user.Email!, BuildPasswordResetEmail(user.FirstName, user.Id, code));
        await _unitOfWork.SaveChangeAsync();
        return new ApiResponse().SetOk("Password reset instructions were queued for the user.");
    }

    private async Task<AuthTokenResponse> IssueSessionAsync(UserAccount user)
    {
        var rawRefreshToken = GenerateOpaqueToken();
        var refreshExpires = DateTime.UtcNow.Add(RefreshTokenLifetime);
        await _unitOfWork.RefreshTokens.AddAsync(new RefreshToken
        {
            UserId = user.Id,
            Token = HashOpaqueToken(rawRefreshToken),
            SessionFamilyId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = refreshExpires
        });
        await _unitOfWork.SaveChangeAsync();
        return CreateSessionResponse(user, rawRefreshToken, refreshExpires);
    }

    private AuthTokenResponse CreateSessionResponse(UserAccount user, string rawRefreshToken, DateTime refreshExpires)
    {
        var accessExpires = DateTime.UtcNow.AddMinutes(_appSettings.SecretToken.DurationInMinutes);
        return new AuthTokenResponse
        {
            AccessToken = CreateToken(user, accessExpires),
            RefreshToken = rawRefreshToken,
            AccessTokenExpiresAt = accessExpires,
            RefreshTokenExpiresAt = refreshExpires
        };
    }

    private string CreateToken(UserAccount user, DateTime expiresAt)
    {
        var fullName = $"{user.FirstName} {user.LastName}".Trim();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString(CultureInfo.InvariantCulture)),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("Role", user.Role.ToString()),
            new("Email", user.Email!),
            new("UserId", user.Id.ToString(CultureInfo.InvariantCulture)),
            new("FullName", fullName),
            new(ClaimTypes.Name, fullName),
            new("pwd", user.PasswordChangedAt.Ticks.ToString(CultureInfo.InvariantCulture)),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_appSettings.SecretToken.Value));
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            _appSettings.SecretToken.Issuer,
            _appSettings.SecretToken.Audience,
            claims,
            expires: expiresAt,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha512Signature)));
    }

    private async Task<ApiResponse> SetPasswordAsync(UserAccount user, string password, EmailVerification? consumedToken)
    {
        await _unitOfWork.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        try
        {
            var hash = CreatePasswordHash(password);
            user.PasswordHash = hash.PasswordHash;
            user.PasswordSalt = hash.PasswordSalt;
            user.PasswordChangedAt = DateTime.UtcNow;
            user.FailedLoginAttempts = 0;
            user.LockoutEnd = null;
            if (consumedToken != null) consumedToken.IsUsed = true;
            await RevokeAllRefreshTokensAsync(user.Id);
            await _unitOfWork.SaveChangeAsync();
            await _unitOfWork.CommitTransactionAsync();
            return new ApiResponse().SetOk("Password changed. Sign in again on all devices.");
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    private async Task RevokeAllRefreshTokensAsync(int userId)
    {
        var tokens = await _unitOfWork.RefreshTokens.GetAllAsync(x => x.UserId == userId && !x.IsRevoked);
        foreach (var token in tokens)
        {
            token.IsRevoked = true;
            token.RevokedAt = DateTime.UtcNow;
            _unitOfWork.RefreshTokens.Update(token);
        }
    }

    private async Task ReplaceSecurityTokensAsync(int userId, string purpose, string newCode)
    {
        await _unitOfWork.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        try
        {
            var previous = await _unitOfWork.EmailVerifications.GetAllAsync(x =>
                x.UserId == userId && x.Purpose == purpose && !x.IsUsed);
            foreach (var token in previous)
            {
                token.IsUsed = true;
                _unitOfWork.EmailVerifications.Update(token);
            }
            await _unitOfWork.EmailVerifications.AddAsync(CreateSecurityToken(userId, newCode, purpose));
            await _unitOfWork.SaveChangeAsync();
            await _unitOfWork.CommitTransactionAsync();
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    private async Task<(bool IsValid, EmailVerification? Record, ApiResponse Response)> ValidateSecurityCodeAsync(
        int userId, string code, string purpose)
    {
        var invalid = purpose == SecurityTokenPurposes.PasswordReset
            ? BadRequest("Invalid or expired reset token.")
            : BadRequest("Invalid or expired verification code.");
        if (userId <= 0 || string.IsNullOrWhiteSpace(code)) return (false, null, invalid);
        var record = await _unitOfWork.EmailVerifications.GetAsync(x =>
            x.UserId == userId && x.Purpose == purpose && !x.IsUsed);
        if (record == null || record.ExpiresAt <= DateTime.UtcNow) return (false, record, invalid);

        var supplied = HashSecurityCode(userId, purpose, code.Trim());
        var matches = FixedTimeEquals(record.VerificationCode, supplied);
        if (!matches)
        {
            record.FailedAttempts++;
            if (record.FailedAttempts >= MaximumFailedCodes) record.IsUsed = true;
            await _unitOfWork.SaveChangeAsync();
            return (false, record, invalid);
        }
        return (true, record, new ApiResponse().SetOk());
    }

    private EmailVerification CreateSecurityToken(int userId, string code, string purpose)
    {
        var lifetime = purpose == SecurityTokenPurposes.EmailVerification
            ? EmailVerificationCodeLifetime
            : PasswordResetCodeLifetime;
        return new EmailVerification
        {
            UserId = userId,
            Purpose = purpose,
            VerificationCode = HashSecurityCode(userId, purpose, code),
            ExpiresAt = DateTime.UtcNow.Add(lifetime),
            IsUsed = false
        };
    }

    private async Task QueueEmailAsync(string recipient, string htmlBody)
    {
        await _unitOfWork.EmailOutboxMessages.AddAsync(new EmailOutboxMessage
        {
            Recipient = recipient.Trim(),
            ProtectedHtmlBody = ProtectedPayload.Protect(htmlBody, _appSettings.SecretToken.Value, "email-outbox"),
            CreatedAt = DateTime.UtcNow,
            NextAttemptAt = DateTime.UtcNow
        });
    }

    private string HashSecurityCode(int userId, string purpose, string code)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_appSettings.SecretToken.Value));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{purpose}:{userId}:{code}")))[..20];
    }

    private async Task<UserAccount?> FindByNormalizedEmailAsync(string normalizedEmail) =>
        await _unitOfWork.UserAccounts.GetAsync(x =>
            x.NormalizedEmail == normalizedEmail || (x.Email != null && x.Email.ToUpper() == normalizedEmail));

    private PasswordDTO CreatePasswordHash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(32);
        return new PasswordDTO
        {
            PasswordSalt = salt,
            PasswordHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, PasswordIterations, HashAlgorithmName.SHA512, 64)
        };
    }

    private static bool VerifyPasswordHash(string password, byte[] hash, byte[] salt)
    {
        byte[] computed;
        if (IsLegacyPasswordHash(salt))
        {
            using var hmac = new HMACSHA512(salt);
            computed = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
        }
        else
            computed = Rfc2898DeriveBytes.Pbkdf2(password, salt, PasswordIterations, HashAlgorithmName.SHA512, 64);
        return computed.Length == hash.Length && CryptographicOperations.FixedTimeEquals(computed, hash);
    }

    private static bool IsLegacyPasswordHash(byte[] salt) => salt.Length != 32;
    private static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();
    private static string GenerateSecurityCode() => RandomNumberGenerator.GetInt32(100000, 1000000).ToString(CultureInfo.InvariantCulture);
    private static string GenerateOpaqueToken() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));
    private static string HashOpaqueToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    private static string? SanitizeDeviceInfo(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(500, value.Trim().Length)];
    private static bool FixedTimeEquals(string left, string right) =>
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(left), Encoding.ASCII.GetBytes(right));

    private static bool IsStrongPassword(string password) =>
        !string.IsNullOrWhiteSpace(password) && password.Length is >= 10 and <= 128 &&
        password.Any(char.IsUpper) && password.Any(char.IsLower) && password.Any(char.IsDigit);

    private static string BuildVerificationEmail(string? firstName, string code) =>
        $"Dear {WebUtility.HtmlEncode(firstName)},<br/>Use this verification code: <strong>{code}</strong>.<br/>It expires in 5 minutes.";

    private static string BuildPasswordResetEmail(string? firstName, int userId, string code) =>
        $"Dear {WebUtility.HtmlEncode(firstName)},<br/>Your BuildSense password reset user ID is {userId} and code is <strong>{code}</strong>.<br/>It expires in 30 minutes.";

    private static ApiResponse Unauthorized() =>
        new ApiResponse().SetApiResponse(HttpStatusCode.Unauthorized, false, "Authentication failed.");
    private static ApiResponse BadRequest(string message) => new ApiResponse().SetBadRequest(message);

    public bool CheckUserPassword(UserRegisterRequest user) => user.Password == user.ConfirmPassword;

    public sealed class PasswordDTO
    {
        public byte[] PasswordHash { get; init; } = Array.Empty<byte>();
        public byte[] PasswordSalt { get; init; } = Array.Empty<byte>();
    }
}
