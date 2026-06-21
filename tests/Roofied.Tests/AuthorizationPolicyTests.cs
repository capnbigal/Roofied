using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Roofied.Application.Authorization;
using Roofied.Domain.Identity;
using Roofied.Web.Security;

namespace Roofied.Tests;

public class AuthorizationPolicyTests
{
    private static IAuthorizationService BuildAuthz()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorizationBuilder().AddRoofiedPolicies();
        return services.BuildServiceProvider().GetRequiredService<IAuthorizationService>();
    }

    private static ClaimsPrincipal UserWith(params string[] roles)
    {
        var identity = new ClaimsIdentity(roles.Select(r => new Claim(ClaimTypes.Role, r)), "test");
        return new ClaimsPrincipal(identity);
    }

    private static ClaimsPrincipal Anonymous() => new(new ClaimsIdentity());

    [Fact]
    public async Task Administrator_only_requires_admin_role()
    {
        var authz = BuildAuthz();
        Assert.True((await authz.AuthorizeAsync(UserWith(RoleNames.Administrator), null, PolicyNames.AdministratorOnly)).Succeeded);
        Assert.False((await authz.AuthorizeAsync(UserWith(RoleNames.Moderator), null, PolicyNames.AdministratorOnly)).Succeeded);
        Assert.False((await authz.AuthorizeAsync(Anonymous(), null, PolicyNames.AdministratorOnly)).Succeeded);
    }

    [Theory]
    [InlineData(PolicyNames.CanModerateReports)]
    [InlineData(PolicyNames.CanViewRestrictedReportData)]
    [InlineData(PolicyNames.CanModerateChannels)]
    [InlineData(PolicyNames.ModeratorOrAdministrator)]
    public async Task Moderator_policies_allow_moderator_and_admin_but_not_others(string policy)
    {
        var authz = BuildAuthz();
        Assert.True((await authz.AuthorizeAsync(UserWith(RoleNames.Moderator), null, policy)).Succeeded);
        Assert.True((await authz.AuthorizeAsync(UserWith(RoleNames.Administrator), null, policy)).Succeeded);
        Assert.False((await authz.AuthorizeAsync(UserWith(RoleNames.RegisteredUser), null, policy)).Succeeded);
        Assert.False((await authz.AuthorizeAsync(Anonymous(), null, policy)).Succeeded);
    }

    [Fact]
    public async Task Admin_platform_policy_excludes_moderators()
    {
        var authz = BuildAuthz();
        Assert.True((await authz.AuthorizeAsync(UserWith(RoleNames.Administrator), null, PolicyNames.CanAdministerPlatform)).Succeeded);
        Assert.False((await authz.AuthorizeAsync(UserWith(RoleNames.Moderator), null, PolicyNames.CanAdministerPlatform)).Succeeded);
    }

    [Fact]
    public async Task Registered_user_policy_requires_authentication()
    {
        var authz = BuildAuthz();
        var authenticated = new ClaimsPrincipal(new ClaimsIdentity(Array.Empty<Claim>(), "test"));
        Assert.True((await authz.AuthorizeAsync(authenticated, null, PolicyNames.RegisteredUser)).Succeeded);
        Assert.False((await authz.AuthorizeAsync(Anonymous(), null, PolicyNames.RegisteredUser)).Succeeded);
    }
}
