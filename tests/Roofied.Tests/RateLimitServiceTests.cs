using Microsoft.Extensions.Options;
using Roofied.Domain.Enums;
using Roofied.Infrastructure.Options;
using Roofied.Infrastructure.Services;

namespace Roofied.Tests;

public class RateLimitServiceTests
{
    [Fact]
    public async Task Blocks_after_limit_is_reached_within_window()
    {
        var clock = new FakeClock();
        var factory = new TestDbContextFactory(Guid.NewGuid().ToString(), clock);
        var options = Options.Create(new RateLimitOptions
        {
            ReportSubmit = new RateLimitRule { Limit = 3, WindowMinutes = 60 },
        });
        var svc = new RateLimitService(factory, clock, options);

        for (var i = 0; i < 3; i++)
        {
            var ok = await svc.CheckAndRecordAsync(RateLimitAction.ReportSubmit, "client-1");
            Assert.True(ok.Allowed);
        }

        var blocked = await svc.CheckAndRecordAsync(RateLimitAction.ReportSubmit, "client-1");
        Assert.False(blocked.Allowed);
    }

    [Fact]
    public async Task Different_clients_are_limited_independently()
    {
        var clock = new FakeClock();
        var factory = new TestDbContextFactory(Guid.NewGuid().ToString(), clock);
        var options = Options.Create(new RateLimitOptions
        {
            ReportSubmit = new RateLimitRule { Limit = 1, WindowMinutes = 60 },
        });
        var svc = new RateLimitService(factory, clock, options);

        Assert.True((await svc.CheckAndRecordAsync(RateLimitAction.ReportSubmit, "a")).Allowed);
        Assert.False((await svc.CheckAndRecordAsync(RateLimitAction.ReportSubmit, "a")).Allowed);
        Assert.True((await svc.CheckAndRecordAsync(RateLimitAction.ReportSubmit, "b")).Allowed);
    }

    [Fact]
    public async Task Window_resets_allow_after_time_passes()
    {
        var clock = new FakeClock();
        var factory = new TestDbContextFactory(Guid.NewGuid().ToString(), clock);
        var options = Options.Create(new RateLimitOptions
        {
            ReportSubmit = new RateLimitRule { Limit = 1, WindowMinutes = 15 },
        });
        var svc = new RateLimitService(factory, clock, options);

        Assert.True((await svc.CheckAndRecordAsync(RateLimitAction.ReportSubmit, "a")).Allowed);
        Assert.False((await svc.CheckAndRecordAsync(RateLimitAction.ReportSubmit, "a")).Allowed);

        clock.UtcNow = clock.UtcNow.AddMinutes(20); // move past the window
        Assert.True((await svc.CheckAndRecordAsync(RateLimitAction.ReportSubmit, "a")).Allowed);
    }
}
