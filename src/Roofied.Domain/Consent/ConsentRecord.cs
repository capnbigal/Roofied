using Roofied.Domain.Common;

namespace Roofied.Domain.Consent;

/// <summary>
/// Records that a user acknowledged consent/safety text at a point in time, with the exact
/// version of the text they agreed to. Supports anonymous submissions (no user id).
/// </summary>
public class ConsentRecord : AuditableEntity
{
    /// <summary>e.g. "ReportSubmission", "ChannelPosting".</summary>
    public required string ConsentType { get; set; }

    /// <summary>Version identifier of the consent/acknowledgement text shown.</summary>
    public required string ConsentTextVersion { get; set; }

    /// <summary>Snapshot of the acknowledged text (for legal traceability).</summary>
    public string? AcknowledgedText { get; set; }

    /// <summary>Null for anonymous submissions.</summary>
    public string? UserId { get; set; }

    /// <summary>Optionally links the consent to a specific report.</summary>
    public Guid? ReportId { get; set; }

    /// <summary>Salted hash of submitter IP (never the raw IP).</summary>
    public string? IpHash { get; set; }
}
