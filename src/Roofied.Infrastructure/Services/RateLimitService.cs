using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Roofied.Application.Abstractions;
using Roofied.Domain.Abuse;
using Roofied.Domain.Enums;
using Roofied.Infrastructure.Options;
using Roofied.Infrastructure.Persistence;

namespace Roofied.Infrastructure.Services;

/// <summary>
/// Durable sliding-window rate limiter backed by the database, keyed by a hashed client identifier.
/// Complements the ASP.NET in-memory rate limiter middleware (which guards raw request volume).
/// </summary>
public sealed class RateLimitService(RoofiedDbContext db, IClock clock, IOptions<RateLimitOptions> options)
    : IRateLimitService
{
    private readonly RateLimitOptions _options = options.Value;

    public async Task<RateLimitResult> CheckAndRecordAsync(
        RateLimitAction action, string clientKey, CancellationToken ct = default)
    {
        var rule = Resolve(action);
        var now = clock.UtcNow;
        var windowStart = now.AddMinutes(-rule.WindowMinutes);

        var count = await db.AbuseRateLimitEvents
            .Where(e => e.Action == action && e.ClientKey == clientKey && e.OccurredUtc >= windowStart)
            .CountAsync(ct);

        var allowed = count < rule.Limit;

        db.AbuseRateLimitEvents.Add(new AbuseRateLimitEvent
        {
            Action = action,
            ClientKey = clientKey,
            OccurredUtc = now,
            WasBlocked = !allowed,
        });
        await db.SaveChangesAsync(ct);

        if (!allowed)
            return RateLimitResult.Blocked(TimeSpan.FromMinutes(rule.WindowMinutes));

        return RateLimitResult.Ok(Math.Max(0, rule.Limit - count - 1));
    }

    private RateLimitRule Resolve(RateLimitAction action) => action switch
    {
        RateLimitAction.ReportSubmit => _options.ReportSubmit,
        RateLimitAction.ChannelPost => _options.ChannelPost,
        RateLimitAction.ContentFlag => _options.ContentFlag,
        RateLimitAction.SignIn => _options.SignIn,
        RateLimitAction.AccountRegister => _options.AccountRegister,
        _ => new RateLimitRule { Limit = 10, WindowMinutes = 60 },
    };
}
