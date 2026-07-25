using cpms_Application.Response;
using cpms_Domain.Models;
using cpms_Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace cpms_API.Middleware;

public sealed class DistributedAuthRateLimitMiddleware
{
    private const int PermitLimit = 5;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);
    private readonly RequestDelegate _next;

    public DistributedAuthRateLimitMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, AppDbContext db)
    {
        if (!context.Request.Path.StartsWithSegments("/api/Auth", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var rawKey = $"{context.Connection.RemoteIpAddress}:{context.Request.Path.Value?.ToLowerInvariant()}";
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawKey)));
        var now = DateTime.UtcNow;
        var allowed = false;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, context.RequestAborted);
            try
            {
                var entry = await db.AuthRateLimitEntries.SingleOrDefaultAsync(x => x.PartitionKey == key, context.RequestAborted);
                if (entry == null)
                {
                    entry = new AuthRateLimitEntry { PartitionKey = key, WindowStart = now, RequestCount = 1 };
                    db.AuthRateLimitEntries.Add(entry);
                    allowed = true;
                }
                else if (now - entry.WindowStart >= Window)
                {
                    entry.WindowStart = now;
                    entry.RequestCount = 1;
                    allowed = true;
                }
                else if (entry.RequestCount < PermitLimit)
                {
                    entry.RequestCount++;
                    allowed = true;
                }
                await db.SaveChangesAsync(context.RequestAborted);
                await transaction.CommitAsync(context.RequestAborted);
                break;
            }
            catch (DbUpdateException) when (attempt == 0)
            {
                await transaction.RollbackAsync(context.RequestAborted);
                foreach (var changed in db.ChangeTracker.Entries<AuthRateLimitEntry>().ToList())
                    changed.State = EntityState.Detached;
                allowed = false;
            }
        }

        if (!allowed)
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers.RetryAfter = "60";
            await context.Response.WriteAsJsonAsync(
                new ApiResponse().SetApiResponse((HttpStatusCode)429, false, "Too many authentication requests. Try again later."),
                context.RequestAborted);
            return;
        }
        await _next(context);
    }
}
