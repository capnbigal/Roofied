using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Roofied.Application.Channels;
using Roofied.Application.Channels.Validation;
using Roofied.Application.Geo;
using Roofied.Application.Safety;
using Roofied.Domain.Channels;
using Roofied.Domain.Enums;
using Roofied.Infrastructure.Options;
using Roofied.Infrastructure.Persistence;
using Roofied.Infrastructure.Services;

namespace Roofied.Tests;

public class ChannelModerationTests
{
    private sealed class Harness : IDisposable
    {
        public RoofiedDbContext Db { get; }
        public FakeClock Clock { get; } = new();
        public FakeCurrentUser User { get; } = new();
        public ChannelService Channels { get; }
        public ModerationService Moderation { get; }
        public Guid ChannelId { get; }
        public string Slug { get; }

        public Harness()
        {
            Db = TestDb.Create(Clock);
            var sanitizer = TestDb.Sanitizer();
            var audit = new AuditService(Db);
            var rate = new RateLimitService(Db, Clock, Options.Create(new RateLimitOptions()));
            Channels = new ChannelService(Db, new ChannelPostInputValidator(), sanitizer,
                new PiiDetectionService(), rate, new AlwaysPassCaptcha(), User, audit);
            Moderation = new ModerationService(Db, new LocationPrecisionService(), sanitizer,
                Clock, User, audit, Options.Create(new LocationPrecisionConfig()));

            var channel = new Channel { Name = "Safety Tips", Slug = "safety-tips", AllowAnonymousPosts = true };
            Db.Channels.Add(channel);
            Db.SaveChanges();
            ChannelId = channel.Id;
            Slug = channel.Slug;
        }

        public void Dispose() => Db.Dispose();
    }

    private static ChannelPostInput Post(Guid channelId) => new()
    {
        ChannelId = channelId,
        Title = "Stay aware",
        Body = "Watch your drink and look out for each other.",
        PostAnonymously = true,
        GuidelinesAcknowledged = true,
    };

    [Fact]
    public async Task New_post_is_held_for_moderation_and_not_public()
    {
        using var h = new Harness();
        var result = await h.Channels.CreatePostAsync(Post(h.ChannelId));
        Assert.True(result.Succeeded);

        var post = await h.Db.ChannelPosts.SingleAsync();
        Assert.Equal(ChannelPostStatus.PendingReview, post.Status);

        var feed = await h.Channels.GetPostsAsync(h.Slug, null, 1, 20);
        Assert.Empty(feed.Items);
    }

    [Fact]
    public async Task Approved_post_becomes_public()
    {
        using var h = new Harness();
        await h.Channels.CreatePostAsync(Post(h.ChannelId));
        var post = await h.Db.ChannelPosts.SingleAsync();

        var approve = await h.Moderation.ApprovePostAsync(post.Id);
        Assert.True(approve.Succeeded);

        var feed = await h.Channels.GetPostsAsync(h.Slug, null, 1, 20);
        Assert.Single(feed.Items);
    }

    [Fact]
    public async Task Hidden_post_is_removed_from_feed()
    {
        using var h = new Harness();
        await h.Channels.CreatePostAsync(Post(h.ChannelId));
        var post = await h.Db.ChannelPosts.SingleAsync();
        await h.Moderation.ApprovePostAsync(post.Id);
        Assert.Single((await h.Channels.GetPostsAsync(h.Slug, null, 1, 20)).Items);

        await h.Moderation.HidePostAsync(post.Id);
        Assert.Empty((await h.Channels.GetPostsAsync(h.Slug, null, 1, 20)).Items);
    }

    [Fact]
    public async Task Redacted_body_replaces_public_body()
    {
        using var h = new Harness();
        await h.Channels.CreatePostAsync(Post(h.ChannelId));
        var post = await h.Db.ChannelPosts.SingleAsync();
        await h.Moderation.RedactPostAsync(post.Id, "[redacted by moderator]");
        await h.Moderation.ApprovePostAsync(post.Id);

        var feed = await h.Channels.GetPostsAsync(h.Slug, null, 1, 20);
        Assert.Equal("[redacted by moderator]", feed.Items.Single().Body);
    }
}
