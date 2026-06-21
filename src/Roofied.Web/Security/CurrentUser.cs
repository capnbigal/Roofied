using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Roofied.Application.Abstractions;

namespace Roofied.Web.Security;

/// <summary>
/// Stable per-circuit (or per-request) identifier. Used as a rate-limit / correlation key fallback
/// when a real client IP is not available inside an interactive Blazor Server circuit.
/// </summary>
public sealed class ClientSession
{
    public string Key { get; } = Guid.NewGuid().ToString("N");
}

/// <summary>
/// Implementation of <see cref="ICurrentUser"/> that works in BOTH execution contexts:
/// - During SSR / prerender and HTTP endpoint calls, the principal and client IP come from <c>HttpContext</c>.
/// - During interactive Blazor Server circuit events (where <c>HttpContext</c> is null), the principal
///   comes from the <see cref="AuthenticationStateProvider"/> and the rate-limit key falls back to a
///   per-circuit session id (real per-IP limiting should additionally be enforced at the IIS/WAF layer).
/// </summary>
public sealed class CurrentUser(
    IHttpContextAccessor accessor,
    AuthenticationStateProvider authState,
    IIpHasher ipHasher,
    ClientSession session) : ICurrentUser
{
    public const string DisplayNameClaim = "display_name";

    private ClaimsPrincipal? Principal
    {
        get
        {
            var ctx = accessor.HttpContext;
            if (ctx is not null)
                return ctx.User;

            var task = authState.GetAuthenticationStateAsync();
            return task.IsCompletedSuccessfully ? task.Result.User : null;
        }
    }

    public string? UserId => Principal?.FindFirstValue(ClaimTypes.NameIdentifier);

    public string? DisplayName => Principal?.FindFirstValue(DisplayNameClaim);

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public bool IsInRole(string role) => Principal?.IsInRole(role) ?? false;

    public string? IpHash
    {
        get
        {
            var ip = accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
            return ipHasher.Hash(ip ?? session.Key);
        }
    }
}
