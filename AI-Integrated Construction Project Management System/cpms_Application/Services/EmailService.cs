using cpms_Application.Interfaces;
using cpms_Application.Response;
using cpms_Domain;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace cpms_Application.Services;

public class EmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<EmailService> _logger;
    private readonly HttpClient _httpClient;

    public EmailService(AppSetting appSettings, HttpClient httpClient, ILogger<EmailService> logger)
    {
        _settings = appSettings.Email;
        _httpClient = httpClient;
        _logger = logger;
    }

    public Task<ApiResponse> SendNotiMail(string recievedUser, string emailContent) =>
        SendAsync(recievedUser, emailContent, "Notification");

    public Task<ApiResponse> SendValidationEmail(string recievedUser, string emailContent) =>
        SendAsync(recievedUser, emailContent, "Verification Email");

    private async Task<ApiResponse> SendAsync(string recievedUser, string emailContent, string subject)
    {
        try
        {
            if (!HasGmailApiConfiguration())
                return new ApiResponse().SetBadRequest(message: "Gmail API email configuration is not complete.");

            var recipient = new System.Net.Mail.MailAddress(recievedUser.Trim()).Address;
            var accessToken = await GetAccessTokenAsync();
            var mimeMessage = BuildMimeMessage(recipient, emailContent, subject);
            var payload = JsonSerializer.Serialize(new
            {
                raw = Base64UrlEncode(Encoding.UTF8.GetBytes(mimeMessage))
            });
            using var request = new HttpRequestMessage(HttpMethod.Post, "gmail/v1/users/me/messages/send")
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Gmail API rejected email delivery for {Recipient} with status {StatusCode}: {ResponseBody}",
                    recievedUser, (int)response.StatusCode, responseBody);
                return new ApiResponse().SetApiResponse(
                    System.Net.HttpStatusCode.ServiceUnavailable, false, "Gmail rejected the email message.");
            }

            return new ApiResponse().SetOk("Mail Sent!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Gmail API email delivery failed for {Recipient}.", recievedUser);
            return new ApiResponse().SetApiResponse(
                System.Net.HttpStatusCode.ServiceUnavailable, false, "Unable to send email.");
        }
    }

    private bool HasGmailApiConfiguration() =>
        !string.IsNullOrWhiteSpace(_settings.GmailClientId) &&
        !string.IsNullOrWhiteSpace(_settings.GmailClientSecret) &&
        !string.IsNullOrWhiteSpace(_settings.GmailRefreshToken) &&
        !string.IsNullOrWhiteSpace(_settings.GmailSenderEmail);

    private async Task<string> GetAccessTokenAsync()
    {
        var form = new Dictionary<string, string>
        {
            ["client_id"] = _settings.GmailClientId.Trim(),
            ["client_secret"] = _settings.GmailClientSecret.Trim(),
            ["refresh_token"] = _settings.GmailRefreshToken.Trim(),
            ["grant_type"] = "refresh_token"
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/token")
        {
            Content = new FormUrlEncodedContent(form)
        };
        using var response = await _httpClient.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Google OAuth token refresh failed with status {(int)response.StatusCode}: {responseBody}");

        using var document = JsonDocument.Parse(responseBody);
        if (!document.RootElement.TryGetProperty("access_token", out var accessToken) ||
            string.IsNullOrWhiteSpace(accessToken.GetString()))
            throw new InvalidOperationException("Google OAuth token response did not include an access token.");

        return accessToken.GetString()!;
    }

    private string BuildMimeMessage(string recipient, string htmlBody, string subject)
    {
        var sender = new System.Net.Mail.MailAddress(_settings.GmailSenderEmail.Trim()).Address;
        var senderName = _settings.GmailSenderName.Trim();
        var encodedSubject = Convert.ToBase64String(Encoding.UTF8.GetBytes(subject));
        var encodedBody = Convert.ToBase64String(Encoding.UTF8.GetBytes(htmlBody));
        return $"From: {senderName} <{sender}>\r\n" +
               $"To: {recipient}\r\n" +
               $"Subject: =?UTF-8?B?{encodedSubject}?=\r\n" +
               "MIME-Version: 1.0\r\n" +
               "Content-Type: text/html; charset=utf-8\r\n" +
               "Content-Transfer-Encoding: base64\r\n\r\n" +
               encodedBody;
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
