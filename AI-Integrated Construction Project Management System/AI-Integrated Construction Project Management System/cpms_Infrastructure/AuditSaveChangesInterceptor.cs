using cpms_Domain.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Text.Json;

namespace cpms_Infrastructure;

public sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly List<PendingAddedAudit> _pendingAdded = new();
    private bool _writingDeferredAudits;
    public AuditSaveChangesInterceptor(IHttpContextAccessor httpContextAccessor) => _httpContextAccessor = httpContextAccessor;

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        AddAuditEntries(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        WriteDeferredAudits(eventData.Context, async: false).GetAwaiter().GetResult();
        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
        CancellationToken cancellationToken = default)
    {
        await WriteDeferredAudits(eventData.Context, async: true, cancellationToken);
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        AddAuditEntries(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void AddAuditEntries(DbContext? context)
    {
        if (context == null || _writingDeferredAudits) return;
        var httpContext = _httpContextAccessor.HttpContext;
        if (!int.TryParse(httpContext?.User.FindFirst("UserId")?.Value, out var userId)) return;
        var candidates = context.ChangeTracker.Entries()
            .Where(entry => entry.Entity is not ActivityLog && entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();
        foreach (var entry in candidates)
        {
            if (entry.State == EntityState.Added)
            {
                _pendingAdded.Add(new PendingAddedAudit(context, entry, userId, httpContext!.TraceIdentifier));
                continue;
            }
            var key = entry.Metadata.FindPrimaryKey();
            var entityId = key == null ? null : string.Join(",", key.Properties.Select(property => entry.Property(property.Name).CurrentValue));
            var changes = entry.Properties
                .Where(property => entry.State != EntityState.Modified || property.IsModified)
                .Where(property => !IsSensitive(property.Metadata.Name))
                .ToDictionary(property => property.Metadata.Name, property => new
                {
                    Before = entry.State == EntityState.Added ? null : property.OriginalValue,
                    After = entry.State == EntityState.Deleted ? null : property.CurrentValue
                });
            context.Set<ActivityLog>().Add(new ActivityLog
            {
                UserID = userId,
                ActivityName = entry.State.ToString().ToUpperInvariant(),
                EntityType = entry.Metadata.ClrType.Name,
                EntityId = entityId,
                ChangesJson = JsonSerializer.Serialize(changes),
                CorrelationId = httpContext!.TraceIdentifier,
                CreatedDate = DateTime.UtcNow
            });
        }
    }

    private async Task WriteDeferredAudits(DbContext? context, bool async, CancellationToken cancellationToken = default)
    {
        if (context == null || _writingDeferredAudits) return;
        var pending = _pendingAdded.Where(x => ReferenceEquals(x.Context, context)).ToList();
        if (pending.Count == 0) return;
        _pendingAdded.RemoveAll(x => ReferenceEquals(x.Context, context));
        foreach (var item in pending)
        {
            var entry = item.Entry;
            var key = entry.Metadata.FindPrimaryKey();
            var entityId = key == null ? null : string.Join(",", key.Properties.Select(p => entry.Property(p.Name).CurrentValue));
            var changes = entry.Properties
                .Where(property => !IsSensitive(property.Metadata.Name))
                .ToDictionary(property => property.Metadata.Name, property => new { Before = (object?)null, After = property.CurrentValue });
            context.Set<ActivityLog>().Add(new ActivityLog
            {
                UserID = item.UserId,
                ActivityName = "ADDED",
                EntityType = entry.Metadata.ClrType.Name,
                EntityId = entityId,
                ChangesJson = JsonSerializer.Serialize(changes),
                CorrelationId = item.CorrelationId,
                CreatedDate = DateTime.UtcNow
            });
        }
        _writingDeferredAudits = true;
        try
        {
            if (async) await context.SaveChangesAsync(cancellationToken);
            else context.SaveChanges();
        }
        finally { _writingDeferredAudits = false; }
    }

    private sealed record PendingAddedAudit(DbContext Context, EntityEntry Entry, int UserId, string CorrelationId);

    private static bool IsSensitive(string name) =>
        name.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Token", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Secret", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Salt", StringComparison.OrdinalIgnoreCase);
}
