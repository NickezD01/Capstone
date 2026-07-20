using cpms_Domain.Models;
using cpms_Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace cpms_API.BackgroundServices;

public sealed class ProjectStatusWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProjectStatusWorker> _logger;

    public ProjectStatusWorker(IServiceScopeFactory scopeFactory, ILogger<ProjectStatusWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await UpdateDelayedProjectsAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await UpdateDelayedProjectsAsync(stoppingToken);
    }

    private async Task UpdateDelayedProjectsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var updated = await db.Projects
                .Where(project => project.BaselineEnd < DateTime.UtcNow &&
                    project.Status == ProjectStatus.IN_PROGRESS)
                .ExecuteUpdateAsync(setters => setters.SetProperty(project => project.Status, ProjectStatus.DELAYED), cancellationToken);
            if (updated > 0) _logger.LogInformation("Marked {ProjectCount} overdue projects as DELAYED.", updated);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Automatic project-delay evaluation failed.");
        }
    }
}
