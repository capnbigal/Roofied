using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Roofied.Application.Abstractions;
using Roofied.Application.Admin;
using Roofied.Application.Common;
using Roofied.Domain.Identity;
using Roofied.Infrastructure.Persistence;

namespace Roofied.Infrastructure.Services;

public sealed class UserAdminService(
    RoofiedDbContext db,
    UserManager<ApplicationUser> userManager,
    ICurrentUser currentUser,
    IAuditService audit) : IUserAdminService
{
    public async Task<PagedResult<AdminUserDto>> GetUsersAsync(string? search, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Users.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(u => (u.Email != null && EF.Functions.Like(u.Email, $"%{term}%")));
        }

        var total = await query.CountAsync(ct);
        var users = await query.OrderBy(u => u.Email).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        var userIds = users.Select(u => u.Id).ToList();
        var profiles = await db.UserProfiles.AsNoTracking()
            .Where(p => userIds.Contains(p.UserId))
            .ToDictionaryAsync(p => p.UserId, p => p.DisplayName, ct);

        var items = new List<AdminUserDto>(users.Count);
        foreach (var u in users)
        {
            var roles = await userManager.GetRolesAsync(u);
            items.Add(new AdminUserDto(
                u.Id, u.Email, profiles.GetValueOrDefault(u.Id), u.IsDisabled,
                roles.ToList(), u.CreatedUtc, u.LastSignInUtc));
        }

        return PagedResult<AdminUserDto>.Create(items, page, pageSize, total);
    }

    public async Task<OperationResult> SetRolesAsync(string userId, IReadOnlyList<string> roles, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null) return OperationResult.Fail("User not found.");

        var valid = roles.Where(r => RoleNames.All.Contains(r)).ToList();
        var current = await userManager.GetRolesAsync(user);

        var toAdd = valid.Except(current).ToList();
        var toRemove = current.Except(valid).ToList();

        if (toRemove.Count > 0)
            await userManager.RemoveFromRolesAsync(user, toRemove);
        if (toAdd.Count > 0)
            await userManager.AddToRolesAsync(user, toAdd);

        await audit.LogAsync("User.RolesChanged", currentUser.UserId, currentUser.DisplayName, nameof(ApplicationUser),
            userId, $"Roles set to: {string.Join(", ", valid)}", ipHash: currentUser.IpHash, ct: ct);
        return OperationResult.Success();
    }

    public async Task<OperationResult> SetDisabledAsync(string userId, bool disabled, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null) return OperationResult.Fail("User not found.");
        user.IsDisabled = disabled;
        if (disabled)
            user.LockoutEnd = DateTimeOffset.MaxValue;
        else
            user.LockoutEnd = null;
        await userManager.UpdateAsync(user);
        await audit.LogAsync("User.DisabledChanged", currentUser.UserId, currentUser.DisplayName, nameof(ApplicationUser),
            userId, $"Disabled={disabled}", ipHash: currentUser.IpHash, ct: ct);
        return OperationResult.Success();
    }
}
