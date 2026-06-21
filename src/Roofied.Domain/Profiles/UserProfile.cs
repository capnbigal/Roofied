using Roofied.Domain.Common;
using Roofied.Domain.Identity;

namespace Roofied.Domain.Profiles;

/// <summary>
/// Optional public-safe profile for a registered user. Only <see cref="DisplayName"/> is ever
/// shown publicly; it is never the user's email or real identity.
/// </summary>
public class UserProfile : FullAuditableEntity
{
    public required string UserId { get; set; }
    public ApplicationUser? User { get; set; }

    /// <summary>Optional public alias. Null means the user posts/reports anonymously.</summary>
    public string? DisplayName { get; set; }

    /// <summary>Short, moderated self-description. Optional.</summary>
    public string? Bio { get; set; }
}
