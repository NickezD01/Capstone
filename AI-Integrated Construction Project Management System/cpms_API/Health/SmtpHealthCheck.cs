using cpms_Domain;
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
            using var client = new SmtpClient();
            await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, _settings.UseSsl, cancellationToken);
            await client.AuthenticateAsync(_settings.Username, _settings.Password, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
            return HealthCheckResult.Healthy("SMTP authentication succeeded.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("SMTP health check failed.", ex);
        }
    }
}
