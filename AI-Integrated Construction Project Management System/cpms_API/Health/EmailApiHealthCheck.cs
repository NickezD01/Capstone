using cpms_Domain;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace cpms_API.Health;

public sealed class EmailApiHealthCheck : IHealthCheck
{
    private readonly EmailSettings _settings;
    public EmailApiHealthCheck(AppSetting settings) => _settings = settings.Email;

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.GmailClientId) ||
            string.IsNullOrWhiteSpace(_settings.GmailClientSecret) ||
            string.IsNullOrWhiteSpace(_settings.GmailRefreshToken) ||
            string.IsNullOrWhiteSpace(_settings.GmailSenderEmail))
            return Task.FromResult(HealthCheckResult.Unhealthy("Gmail API email configuration is not complete."));

        return Task.FromResult(HealthCheckResult.Healthy("Gmail API email configuration is present."));
    }
}
