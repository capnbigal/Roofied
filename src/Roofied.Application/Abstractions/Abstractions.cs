using Roofied.Domain.Enums;

namespace Roofied.Application.Abstractions;

/// <summary>Abstracts the system clock so time-dependent logic is testable.</summary>
public interface IClock
{
    DateTime UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}

/// <summary>One-way salted hashing for client identifiers (IP). Raw IPs are never stored.</summary>
public interface IIpHasher
{
    string? Hash(string? ipAddress);
}

/// <summary>HTML/text sanitization to strip scripts and unsafe markup from user input.</summary>
public interface IHtmlSanitizer
{
    /// <summary>Returns plain text with all HTML removed (for narratives, summaries, titles).</summary>
    string SanitizePlainText(string? input);

    /// <summary>Returns sanitized HTML allowing a safe subset of tags (for rich admin content).</summary>
    string SanitizeHtml(string? input);
}

/// <summary>Generates short, unique, human-friendly report reference codes.</summary>
public interface IReferenceCodeGenerator
{
    string Generate();
}

/// <summary>Bot/abuse verification (e.g. Cloudflare Turnstile). Pluggable per environment.</summary>
public interface ICaptchaVerifier
{
    /// <summary>True if the challenge token is valid (or verification is disabled in this environment).</summary>
    Task<bool> VerifyAsync(string? token, string? remoteIp, CancellationToken ct = default);

    /// <summary>True when a challenge should be rendered (false when disabled / dev no-op).</summary>
    bool IsEnabled { get; }

    /// <summary>Public site key for the client widget, when enabled.</summary>
    string? SiteKey { get; }
}

public sealed record RateLimitResult(bool Allowed, int Remaining, TimeSpan RetryAfter)
{
    public static RateLimitResult Ok(int remaining) => new(true, remaining, TimeSpan.Zero);
    public static RateLimitResult Blocked(TimeSpan retryAfter) => new(false, 0, retryAfter);
}

/// <summary>Durable, action-scoped rate limiting keyed by a hashed client identifier.</summary>
public interface IRateLimitService
{
    Task<RateLimitResult> CheckAndRecordAsync(RateLimitAction action, string clientKey, CancellationToken ct = default);
}

/// <summary>Writes append-only audit entries for security-relevant actions.</summary>
public interface IAuditService
{
    Task LogAsync(
        string action,
        string? actorUserId = null,
        string? actorDisplayName = null,
        string? entityType = null,
        string? entityId = null,
        string? summary = null,
        object? metadata = null,
        string? ipHash = null,
        CancellationToken ct = default);
}

/// <summary>Information about the current request's user, for service-layer authorization/ownership.</summary>
public interface ICurrentUser
{
    string? UserId { get; }
    string? DisplayName { get; }
    bool IsAuthenticated { get; }
    bool IsInRole(string role);
    string? IpHash { get; }
}
