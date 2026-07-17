using cpms_Application.Interfaces;
using cpms_Application.Request.UserAccount;
using cpms_Application.Response;
using cpms_Application.Services;
using cpms_Application.Security;
using cpms_Domain;
using cpms_Domain.Models;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace cpms_Tests;

public class AuthSecurityTests
{
    [Fact]
    public async Task RegistrationUsesPbkdf2AndStoresOnlyHashedVerificationCode()
    {
        var uow = new TestUnitOfWork();
        var email = new CapturingEmailService();
        var service = CreateService(uow, email);

        var registered = await service.RegisterAsync(new UserRegisterRequest
        {
            Email = "User@Example.com",
            Password = "A-strong-test-password-123!",
            ConfirmPassword = "A-strong-test-password-123!",
            FirstName = "Test",
            LastName = "User"
        });

        Assert.True(registered.IsSuccess);
        var user = Assert.Single(uow.UserAccountRecords);
        Assert.Equal("user@example.com", user.Email);
        Assert.Equal(32, user.PasswordSalt.Length);
        Assert.Equal(64, user.PasswordHash.Length);
        var verification = Assert.Single(uow.EmailVerificationRecords);
        Assert.Equal(20, verification.VerificationCode.Length);
        Assert.InRange(verification.ExpiresAt, DateTime.UtcNow.AddMinutes(4).AddSeconds(50), DateTime.UtcNow.AddMinutes(5).AddSeconds(5));
        var queued = Assert.Single(uow.EmailOutboxRecords);
        var queuedBody = ProtectedPayload.Unprotect(queued.ProtectedHtmlBody, new string('k', 64), "email-outbox");
        Assert.Contains("expires in 5 minutes", queuedBody);
        var verificationCode = Regex.Match(queuedBody, @"<strong>(\d{6})</strong>").Groups[1].Value;
        Assert.NotEqual(verificationCode, verification.VerificationCode);

        var verified = await service.VerifyEmailAsync(user.Id, verificationCode);
        Assert.True(verified.IsSuccess);
        Assert.True(user.IsEmailVerified);
    }

    [Fact]
    public async Task SuccessfulLegacyLoginUpgradesPasswordHash()
    {
        var uow = new TestUnitOfWork();
        const string password = "legacy-password";
        using var hmac = new HMACSHA512();
        uow.UserAccountRecords.Add(new UserAccount
        {
            Id = 1,
            Email = "legacy@example.com",
            FirstName = "Legacy",
            LastName = "User",
            IsEmailVerified = true,
            Role = Role.CUSTOMER,
            PasswordSalt = hmac.Key,
            PasswordHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password))
        });

        var loggedIn = await CreateService(uow, new CapturingEmailService()).LoginAsync(new LoginRequest
        {
            UserEmail = "LEGACY@example.com",
            Password = password
        });

        Assert.True(loggedIn.IsSuccess);
        Assert.Equal(32, uow.UserAccountRecords[0].PasswordSalt.Length);
        Assert.Equal(64, uow.UserAccountRecords[0].PasswordHash.Length);
    }

    [Fact]
    public async Task AdministratorCanLoginWithEmailAndPassword()
    {
        var uow = new TestUnitOfWork();
        const string password = "Manager001";
        var salt = RandomNumberGenerator.GetBytes(32);
        uow.UserAccountRecords.Add(new UserAccount
        {
            Id = 1,
            Email = "manager@gmail.com",
            FirstName = "System",
            LastName = "Manager",
            IsEmailVerified = true,
            Role = Role.ADMIN,
            PasswordSalt = salt,
            PasswordHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 210_000, HashAlgorithmName.SHA512, 64),
            PasswordChangedAt = DateTime.UtcNow
        });

        var response = await CreateService(uow, new CapturingEmailService()).LoginAsync(new LoginRequest
        {
            UserEmail = "manager@gmail.com",
            Password = password
        });

        Assert.True(response.IsSuccess, response.ErrorMessage);
    }

    private static AuthService CreateService(TestUnitOfWork uow, IEmailService email) =>
        new(uow, new AppSetting
        {
            SecretToken = new SecretToken
            {
                Value = new string('k', 64),
                Issuer = "tests",
                Audience = "tests",
                DurationInMinutes = 60
            }
        }, new FakeClaimService(1, Role.CUSTOMER), email);
}

internal sealed class CapturingEmailService : IEmailService
{
    public string? VerificationCode { get; private set; }

    public Task<ApiResponse> SendValidationEmail(string recievedUser, string emailContent)
    {
        VerificationCode = Regex.Match(emailContent, @"<strong>(\d{6})</strong>").Groups[1].Value;
        return Task.FromResult(new ApiResponse().SetOk());
    }

    public Task<ApiResponse> SendNotiMail(string recievedUser, string emailContent) =>
        Task.FromResult(new ApiResponse().SetOk());
}
