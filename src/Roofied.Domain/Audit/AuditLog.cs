using Roofied.Domain.Common;

namespace Roofied.Domain.Audit;

/// <summary>
/// Append-only audit record for moderator/administrator actions and other security-relevant events.
/// Must never contain sensitive report text or precise location data.
/// </summary>
public class AuditLog : AuditableEntity
{
    public string? ActorUserId { get; set; }
    public string? ActorDisplayName { get; set; }

    /// <summary>Action verb, e.g. "Report.Approved", "Channel.PostHidden", "User.RoleAssigned".</summary>
    public required string Action { get; set; }

    public string? EntityType { get; set; }
    public string? EntityId { get; set; }

    /// <summary>Short, non-sensitive human-readable summary.</summary>
    public string? Summary { get; set; }

    /// <summary>Optional non-sensitive structured metadata (JSON).</summary>
    public string? MetadataJson { get; set; }

    /// <summary>Salted hash of actor IP (never the raw IP).</summary>
    public string? IpHash { get; set; }
}
