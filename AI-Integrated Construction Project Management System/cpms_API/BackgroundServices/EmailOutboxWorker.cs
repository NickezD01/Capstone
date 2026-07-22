using cpms_Application.Interfaces;
using cpms_Application.Security;
using cpms_Domain;
using cpms_Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace cpms_API.BackgroundServices;

public sealed class EmailOutboxWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AppSetting _settings;
    private readonly ILogger<EmailOutboxWorker> _logger;

    public EmailOutboxWorker(IServiceScopeFactory scopeFactory, AppSetting settings, ILogger<EmailOutboxWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        do
        {
            await ProcessBatchAsync(stoppingToken);
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var sender = scope.ServiceProvider.GetRequiredService<IEmailService>();
            var now = DateTime.UtcNow;
            var messages = await db.EmailOutboxMessages
                .Where(x => x.ProcessedAt == null && x.NextAttemptAt <= now && x.AttemptCount < 10)
                .OrderBy(x => x.MessageId).Take(20).ToListAsync(cancellationToken);
            _logger.LogInformation("Email outbox poll found {Count} pending message(s).", messages.Count);
            foreach (var message in messages)
            {
                try
                {
                    var body = ProtectedPayload.Unprotect(message.ProtectedHtmlBody, _settings.SecretToken.Value, "email-outbox");
                    var response = await sender.SendValidationEmail(message.Recipient, body);
                    message.AttemptCount++;
                    if (response.IsSuccess)
                    {
                        message.ProcessedAt = DateTime.UtcNow;
                        message.LastError = null;
                    }
                    else
                    {
                        message.LastError = response.ErrorMessage ?? "SMTP delivery was rejected.";
                        message.NextAttemptAt = DateTime.UtcNow.AddMinutes(Math.Min(60, Math.Pow(2, message.AttemptCount)));
                        _logger.LogWarning("Email outbox message {MessageId} was rejected: {Error}",
                            message.MessageId, message.LastError);
                    }
                }
                catch (Exception ex)
                {
                    message.AttemptCount++;
                    message.LastError = ex.GetType().Name;
                    message.NextAttemptAt = DateTime.UtcNow.AddMinutes(Math.Min(60, Math.Pow(2, message.AttemptCount)));
                    _logger.LogWarning(ex, "Email outbox message {MessageId} delivery failed.", message.MessageId);
                }
                await db.SaveChangesAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception ex) { _logger.LogError(ex, "Email outbox processing failed."); }
    }
}
