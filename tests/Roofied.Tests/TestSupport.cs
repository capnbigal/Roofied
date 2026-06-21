using Microsoft.EntityFrameworkCore;
using Roofied.Application.Abstractions;
using Roofied.Infrastructure.Persistence;
using Roofied.Infrastructure.Providers;

namespace Roofied.Tests;

public sealed class FakeClock(DateTime? now = null) : IClock
{
    public DateTime UtcNow { get; set; } = now ?? new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
}

public sealed class FakeCurrentUser : ICurrentUser
{
    public string? UserId { get; set; }
    public string? DisplayName { get; set; }
    public bool IsAuthenticated => UserId is not null;
    public string? IpHash { get; set; } = "test-ip-hash";
    private readonly HashSet<string> _roles = new();
    public void AddRole(string role) => _roles.Add(role);
    public bool IsInRole(string role) => _roles.Contains(role);
}

/// <summary>Captcha that always passes; lets us exercise the rest of the pipeline.</summary>
public sealed class AlwaysPassCaptcha : ICaptchaVerifier
{
    public bool IsEnabled => false;
    public string? SiteKey => null;
    public Task<bool> VerifyAsync(string? token, string? remoteIp, CancellationToken ct = default) => Task.FromResult(true);
}

public static class TestDb
{
    /// <summary>Creates a fresh in-memory context with a unique store per call.</summary>
    public static RoofiedDbContext Create(IClock clock, string? name = null)
    {
        var options = new DbContextOptionsBuilder<RoofiedDbContext>()
            .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new RoofiedDbContext(options, clock);
        db.Database.EnsureCreated();
        return db;
    }

    public static IHtmlSanitizer Sanitizer() => new HtmlSanitizerService();
}
