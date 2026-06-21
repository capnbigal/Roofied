using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Ganss.Xss;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Roofied.Application.Abstractions;
using Roofied.Infrastructure.Options;

namespace Roofied.Infrastructure.Providers;

/// <summary>Salted SHA-256 hashing of client IPs. Raw IPs are never persisted.</summary>
public sealed class IpHasher(IOptions<SecurityOptions> options) : IIpHasher
{
    private readonly string _salt = options.Value.IpHashSalt;

    public string? Hash(string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
            return null;
        var bytes = Encoding.UTF8.GetBytes(_salt + "|" + ipAddress.Trim());
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}

/// <summary>Wraps Ganss HtmlSanitizer. Plain-text path strips all markup.</summary>
public sealed class HtmlSanitizerService : Roofied.Application.Abstractions.IHtmlSanitizer
{
    private readonly HtmlSanitizer _rich;
    private readonly HtmlSanitizer _plain;

    public HtmlSanitizerService()
    {
        _rich = new HtmlSanitizer();
        // Conservative allow-list for admin-managed rich content.
        _rich.AllowedTags.Clear();
        foreach (var tag in new[] { "p", "br", "b", "strong", "i", "em", "ul", "ol", "li", "a", "h3", "h4", "blockquote" })
            _rich.AllowedTags.Add(tag);
        _rich.AllowedAttributes.Clear();
        _rich.AllowedAttributes.Add("href");
        _rich.AllowedSchemes.Clear();
        _rich.AllowedSchemes.Add("https");
        _rich.AllowedSchemes.Add("mailto");
        _rich.AllowedSchemes.Add("tel");

        _plain = new HtmlSanitizer();
        _plain.AllowedTags.Clear();
        _plain.AllowedAttributes.Clear();
    }

    public string SanitizePlainText(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;
        // Remove all tags, then decode entities to readable text.
        var stripped = _plain.Sanitize(input);
        return System.Net.WebUtility.HtmlDecode(stripped).Trim();
    }

    public string SanitizeHtml(string? input) =>
        string.IsNullOrWhiteSpace(input) ? string.Empty : _rich.Sanitize(input);
}

/// <summary>Cloudflare Turnstile verifier. When disabled, verification always succeeds (dev no-op).</summary>
public sealed class TurnstileCaptchaVerifier(
    HttpClient httpClient,
    IOptions<CaptchaOptions> options,
    ILogger<TurnstileCaptchaVerifier> logger) : ICaptchaVerifier
{
    private readonly CaptchaOptions _options = options.Value;

    public bool IsEnabled => _options.Enabled;
    public string? SiteKey => _options.SiteKey;

    public async Task<bool> VerifyAsync(string? token, string? remoteIp, CancellationToken ct = default)
    {
        if (!_options.Enabled)
            return true; // dev / disabled

        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(_options.SecretKey))
            return false;

        try
        {
            var form = new Dictionary<string, string>
            {
                ["secret"] = _options.SecretKey!,
                ["response"] = token,
            };
            if (!string.IsNullOrWhiteSpace(remoteIp))
                form["remoteip"] = remoteIp;

            using var response = await httpClient.PostAsync(_options.VerifyUrl, new FormUrlEncodedContent(form), ct);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<TurnstileResponse>(ct);
            return result?.Success == true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Captcha verification failed; rejecting submission.");
            return false;
        }
    }

    private sealed record TurnstileResponse(bool Success);
}
