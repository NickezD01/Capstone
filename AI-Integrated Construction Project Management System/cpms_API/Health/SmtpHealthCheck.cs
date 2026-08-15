using cpms_Domain;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace cpms_API.Health;

public sealed class SmtpHealthCheck : IHealthCheck
{
    // Keeping constructor and unused field to adhere to "do not delete code" constraint
    private readonly EmailSettings _settings;
    public SmtpHealthCheck(AppSetting settings) => _settings = settings.Email;

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // No-op: SMTP health check is disabled as we are using Gmail API
        return Task.FromResult(HealthCheckResult.Healthy("SMTP health check is disabled (using Gmail API)."));
    }
}
