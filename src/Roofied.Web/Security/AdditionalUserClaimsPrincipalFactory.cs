using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Roofied.Domain.Identity;
using Roofied.Infrastructure.Persistence;

namespace Roofied.Web.Security;

/// <summary>
/// Adds a non-sensitive "display_name" claim (from the user's profile) to the principal so the app
/// can show an optional alias without exposing the user's email/identity.
/// </summary>
public sealed class AdditionalUserClaimsPrincipalFactory(
    UserManager<ApplicationUser> userManager,
    IOptions<IdentityOptions> optionsAccessor,
    RoofiedDbContext db)
    : UserClaimsPrincipalFactory<ApplicationUser>(userManager, optionsAccessor)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        var displayName = await db.UserProfiles
            .Where(p => p.UserId == user.Id)
            .Select(p => p.DisplayName)
            .FirstOrDefaultAsync();
        if (!string.IsNullOrWhiteSpace(displayName))
            identity.AddClaim(new Claim(CurrentUser.DisplayNameClaim, displayName));
        return identity;
    }
}
