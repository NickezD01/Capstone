using cpms_Application.Interfaces;
using cpms_Application.Response;
using cpms_Domain;
using MailKit.Net.Smtp;
using MimeKit;
using Microsoft.Extensions.Logging;

namespace cpms_Application.Services;

public class EmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<EmailService> _logger;
    private readonly string _username;
    private readonly string _password;
    private readonly string _fromAddress;

    public EmailService(AppSetting appSettings, ILogger<EmailService> logger)
    {
        _settings = appSettings.Email;
        _logger = logger;
        _username = SmtpConnectionOptions.NormalizeUsername(_settings.Username);
        _password = SmtpConnectionOptions.NormalizePassword(_settings.Password);
        _fromAddress = SmtpConnectionOptions.ResolveFromAddress(_settings, _username);
    }

    public Task<ApiResponse> SendNotiMail(string recievedUser, string emailContent) =>
        SendAsync(recievedUser, emailContent, "Notification");

    public Task<ApiResponse> SendValidationEmail(string recievedUser, string emailContent) =>
        SendAsync(recievedUser, emailContent, "Verification Email");

    private async Task<ApiResponse> SendAsync(string recievedUser, string emailContent, string subject)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_username) || string.IsNullOrWhiteSpace(_password))
                return new ApiResponse().SetBadRequest(message: "SMTP credentials are not configured.");

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("BuildSense", _fromAddress));
            message.To.Add(new MailboxAddress(string.Empty, recievedUser.Trim()));
            message.Subject = subject;

            var bodyBuilder = new BodyBuilder { HtmlBody = emailContent };
            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            var socketOptions = SmtpConnectionOptions.Resolve(_settings);
            _logger.LogInformation("Connecting to SMTP {Host}:{Port} using {Security}.",
                _settings.SmtpHost, _settings.SmtpPort, socketOptions);

            await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, socketOptions);
            _logger.LogInformation("Authenticating SMTP user {Username}.", _username);
            await client.AuthenticateAsync(_username, _password);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            return new ApiResponse().SetOk("Mail Sent!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "SMTP delivery failed at {Host}:{Port} using {Security} for {Recipient}.",
                _settings.SmtpHost, _settings.SmtpPort,
                SmtpConnectionOptions.Resolve(_settings), recievedUser);
            return new ApiResponse().SetApiResponse(
                System.Net.HttpStatusCode.ServiceUnavailable, false, "Unable to send email.");
        }
    }
}
