namespace Roofied.Web.Security;

/// <summary>
/// Adds production-friendly security response headers. The CSP allows the styles/scripts Blazor +
/// MudBlazor + Leaflet need, plus OpenStreetMap tile images. Tune as the app evolves.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["X-Permitted-Cross-Domain-Policies"] = "none";
        headers["Cross-Origin-Opener-Policy"] = "same-origin";
        headers.Append("Permissions-Policy", "geolocation=(self), camera=(), microphone=()");

        // Content Security Policy. 'unsafe-inline'/'unsafe-eval' are required by the current Blazor +
        // MudBlazor + Leaflet client; tighten with nonces/hashes if those constraints relax.
        headers["Content-Security-Policy"] =
            "default-src 'self'; " +
            "img-src 'self' data: https://*.tile.openstreetmap.org https://unpkg.com; " +
            "style-src 'self' 'unsafe-inline' https://unpkg.com https://fonts.googleapis.com; " +
            "font-src 'self' https://fonts.gstatic.com data:; " +
            "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://unpkg.com https://challenges.cloudflare.com; " +
            "connect-src 'self' wss: https:; " +
            "frame-src https://challenges.cloudflare.com; " +
            "frame-ancestors 'none'; base-uri 'self'; form-action 'self';";

        await next(context);
    }
}
