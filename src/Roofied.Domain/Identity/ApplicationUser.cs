using Microsoft.AspNetCore.Identity;
using Roofied.Domain.Profiles;

namespace Roofied.Domain.Identity;

/// <summary>
/// Application user. The email/identity here is private and must never be exposed publicly.
/// Public-facing identity is limited to an optional display name on <see cref="UserProfile"/>.
/// </summary>
public class ApplicationUser : IdentityUser
{
    public DateTime CreatedUtc { get; set; }
    public DateTime? LastSignInUtc { get; set; }

    /// <summary>Soft-disable an account without deleting Identity rows.</summary>
    public bool IsDisabled { get; set; }

    public UserProfile? Profile { get; set; }
}

/// <summary>Well-known role names used across the application.</summary>
public static class RoleNames
{
    public const string Administrator = "Administrator";
    public const string Moderator = "Moderator";
    public const string RegisteredUser = "RegisteredUser";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Administrator,
        Moderator,
        RegisteredUser,
    };
}
