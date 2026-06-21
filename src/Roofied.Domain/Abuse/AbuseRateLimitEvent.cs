using Roofied.Domain.Common;
using Roofied.Domain.Enums;

namespace Roofied.Domain.Abuse;

/// <summary>
/// A persisted record of a rate-limited action attempt, keyed by a hashed client identifier.
/// Used for durable, cross-request abuse throttling (in addition to in-memory limiters).
/// </summary>
public class AbuseRateLimitEvent : AuditableEntity
{
    public RateLimitAction Action { get; set; }

    /// <summary>Hashed client key (IP hash for anonymous, user id for authenticated).</summary>
    public required string ClientKey { get; set; }

    public DateTime OccurredUtc { get; set; }

    /// <summary>True if the attempt was blocked by the limiter.</summary>
    public bool WasBlocked { get; set; }
}
