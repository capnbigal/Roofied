namespace Roofied.Application.Authorization;

/// <summary>
/// Central registry of authorization policy names. Components and endpoints reference these
/// constants instead of scattering raw role checks throughout the codebase.
/// </summary>
public static class PolicyNames
{
    /// <summary>Administrators only.</summary>
    public const string AdministratorOnly = "AdministratorOnly";

    /// <summary>Moderators or administrators (the moderator/admin portal).</summary>
    public const string ModeratorOrAdministrator = "ModeratorOrAdministrator";

    /// <summary>Any signed-in registered user.</summary>
    public const string RegisteredUser = "RegisteredUser";

    /// <summary>Permission to review and moderate reports.</summary>
    public const string CanModerateReports = "CanModerateReports";

    /// <summary>Permission to view restricted (moderator-only) report details.</summary>
    public const string CanViewRestrictedReportData = "CanViewRestrictedReportData";

    /// <summary>Permission to moderate community channel content.</summary>
    public const string CanModerateChannels = "CanModerateChannels";

    /// <summary>Permission to manage users, roles, and platform configuration.</summary>
    public const string CanAdministerPlatform = "CanAdministerPlatform";
}
