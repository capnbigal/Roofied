using Roofied.Domain.Common;
using Roofied.Domain.Enums;

namespace Roofied.Domain.Resources;

/// <summary>
/// An admin-managed help/resource entry shown on the Resources / Get Help Now page.
/// Content is editable through the admin portal rather than hardcoded.
/// </summary>
public class Resource : FullAuditableEntity
{
    public required string Title { get; set; }
    public ResourceCategory Category { get; set; } = ResourceCategory.General;

    /// <summary>Sanitized description / guidance text.</summary>
    public string? Description { get; set; }

    public string? Url { get; set; }
    public string? PhoneNumber { get; set; }

    /// <summary>Optional region scoping; availability and recommendations may vary by region.</summary>
    public string? Region { get; set; }

    /// <summary>Highlighted as emergency guidance (e.g. call emergency services).</summary>
    public bool IsEmergency { get; set; }

    public int SortOrder { get; set; }
    public bool IsPublished { get; set; } = true;

    public string? CreatedByUserId { get; set; }
}
