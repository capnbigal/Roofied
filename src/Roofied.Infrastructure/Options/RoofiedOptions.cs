using System.ComponentModel.DataAnnotations;

namespace Roofied.Infrastructure.Options;

/// <summary>Security-related configuration. Bound from the "Security" section.</summary>
public sealed class SecurityOptions
{
    public const string SectionName = "Security";

    /// <summary>Secret salt used to one-way hash client IPs. REQUIRED in production.</summary>
    [Required]
    public string IpHashSalt { get; set; } = string.Empty;
}

/// <summary>Bot-protection (Cloudflare Turnstile) configuration. Bound from "Captcha".</summary>
public sealed class CaptchaOptions
{
    public const string SectionName = "Captcha";

    /// <summary>When false, captcha verification is a no-op (development).</summary>
    public bool Enabled { get; set; }

    public string? SiteKey { get; set; }
    public string? SecretKey { get; set; }

    public string VerifyUrl { get; set; } = "https://challenges.cloudflare.com/turnstile/v0/siteverify";
}

/// <summary>Default map/location precision. Bound from "LocationPrecision".</summary>
public sealed class LocationPrecisionConfig
{
    public const string SectionName = "LocationPrecision";

    public int DefaultGridSizeMeters { get; set; } = 1500;
    public int MinGridSizeMeters { get; set; } = 500;
}

/// <summary>Per-action rate-limit settings. Bound from "RateLimiting".</summary>
public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimiting";

    public RateLimitRule ReportSubmit { get; set; } = new() { Limit = 5, WindowMinutes = 60 };
    public RateLimitRule ChannelPost { get; set; } = new() { Limit = 10, WindowMinutes = 60 };
    public RateLimitRule ContentFlag { get; set; } = new() { Limit = 20, WindowMinutes = 60 };
    public RateLimitRule SignIn { get; set; } = new() { Limit = 10, WindowMinutes = 15 };
    public RateLimitRule AccountRegister { get; set; } = new() { Limit = 5, WindowMinutes = 60 };
}

public sealed class RateLimitRule
{
    public int Limit { get; set; }
    public int WindowMinutes { get; set; }
}
