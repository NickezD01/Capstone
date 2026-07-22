using cpms_Domain;
using cpms_Application.Services;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace cpms_API.Health;

public sealed class SmtpHealthCheck : IHealthCheck
{
    private readonly EmailSettings _settings;
    public SmtpHealthCheck(AppSetting settings) => _settings = settings.Email;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.Username) || string.IsNullOrWhiteSpace(_settings.Password))
            return HealthCheckResult.Unhealthy("SMTP credentials are not configured.");
        try
        {
            var username = SmtpConnectionOptions.NormalizeUsername(_settings.Username);
            var password = SmtpConnectionOptions.NormalizePassword(_settings.Password);
            var socketOptions = SmtpConnectionOptions.Resolve(_settings);
            using var client = new SmtpClient();
            await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, socketOptions, cancellationToken);
            await client.AuthenticateAsync(username, password, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
            return HealthCheckResult.Healthy($"SMTP authentication succeeded via {socketOptions}.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("SMTP health check failed.", ex);
        }
    }
}
