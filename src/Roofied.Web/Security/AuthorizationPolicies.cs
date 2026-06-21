using Microsoft.AspNetCore.Authorization;
using Roofied.Application.Authorization;
using Roofied.Domain.Identity;

namespace Roofied.Web.Security;

/// <summary>
/// Central registration of all authorization policies. Components/endpoints reference
/// <see cref="PolicyNames"/> rather than scattering raw role checks.
/// </summary>
public static class AuthorizationPolicies
{
    public static AuthorizationBuilder AddRoofiedPolicies(this AuthorizationBuilder builder)
    {
        builder
            .AddPolicy(PolicyNames.AdministratorOnly, p => p.RequireRole(RoleNames.Administrator))
            .AddPolicy(PolicyNames.ModeratorOrAdministrator, p => p.RequireRole(RoleNames.Moderator, RoleNames.Administrator))
            .AddPolicy(PolicyNames.RegisteredUser, p => p.RequireAuthenticatedUser())
            .AddPolicy(PolicyNames.CanModerateReports, p => p.RequireRole(RoleNames.Moderator, RoleNames.Administrator))
            .AddPolicy(PolicyNames.CanViewRestrictedReportData, p => p.RequireRole(RoleNames.Moderator, RoleNames.Administrator))
            .AddPolicy(PolicyNames.CanModerateChannels, p => p.RequireRole(RoleNames.Moderator, RoleNames.Administrator))
            .AddPolicy(PolicyNames.CanAdministerPlatform, p => p.RequireRole(RoleNames.Administrator));
        return builder;
    }
}
