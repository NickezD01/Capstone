using cpms_Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace cpms_API.BackgroundServices;

public sealed class AuthRateLimitCleanupWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuthRateLimitCleanupWorker> _logger;

    public AuthRateLimitCleanupWorker(IServiceScopeFactory scopeFactory, ILogger<AuthRateLimitCleanupWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.AuthRateLimitEntries
                    .Where(x => x.WindowStart < DateTime.UtcNow.AddDays(-1))
                    .ExecuteDeleteAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { _logger.LogError(ex, "Authentication rate-limit cleanup failed."); }
        }
    }
}
