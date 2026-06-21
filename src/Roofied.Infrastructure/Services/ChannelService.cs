using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Roofied.Application.Abstractions;
using Roofied.Application.Channels;
using Roofied.Application.Common;
using Roofied.Application.Safety;
using Roofied.Domain.Channels;
using Roofied.Domain.Enums;
using Roofied.Domain.Moderation;
using Roofied.Infrastructure.Persistence;

namespace Roofied.Infrastructure.Services;

public sealed class ChannelService(
    IDbContextFactory<RoofiedDbContext> dbFactory,
    IValidator<ChannelPostInput> validator,
    IHtmlSanitizer sanitizer,
    IPiiDetectionService pii,
    IRateLimitService rateLimiter,
    ICaptchaVerifier captcha,
    ICurrentUser currentUser,
    IAuditService audit) : IChannelService
{
    public async Task<IReadOnlyList<ChannelDto>> GetChannelsAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Channels.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new ChannelDto(
                c.Id, c.Name, c.Slug, c.Description, c.Guidelines, c.IsLocked, c.AllowAnonymousPosts, c.CommentsEnabled,
                c.Posts.Count(p => p.Status == ChannelPostStatus.Approved && !p.IsHidden)))
            .ToListAsync(ct);
    }

    public async Task<ChannelDto?> GetChannelBySlugAsync(string slug, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Channels.AsNoTracking()
            .Where(c => c.Slug == slug && c.IsActive)
            .Select(c => new ChannelDto(
                c.Id, c.Name, c.Slug, c.Description, c.Guidelines, c.IsLocked, c.AllowAnonymousPosts, c.CommentsEnabled,
                c.Posts.Count(p => p.Status == ChannelPostStatus.Approved && !p.IsHidden)))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<PagedResult<ChannelPostDto>> GetPostsAsync(
        string channelSlug, string? search, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var query = db.ChannelPosts.AsNoTracking()
            .Where(p => p.Channel!.Slug == channelSlug
                        && p.Status == ChannelPostStatus.Approved
                        && !p.IsHidden);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(p => EF.Functions.Like(p.Title, $"%{term}%")
                                     || EF.Functions.Like(p.RedactedBody ?? p.Body, $"%{term}%"));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(p => p.IsPinned).ThenByDescending(p => p.PublishedUtc)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(ToDto())
            .ToListAsync(ct);

        return PagedResult<ChannelPostDto>.Create(items, page, pageSize, total);
    }

    public async Task<ChannelPostDto?> GetPostAsync(Guid postId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.ChannelPosts.AsNoTracking()
            .Where(p => p.Id == postId && p.Status == ChannelPostStatus.Approved && !p.IsHidden)
            .Select(ToDto())
            .FirstOrDefaultAsync(ct);
    }

    public async Task<OperationResult<Guid>> CreatePostAsync(ChannelPostInput input, CancellationToken ct = default)
    {
        var validation = await validator.ValidateAsync(input, ct);
        if (!validation.IsValid)
            return OperationResult<Guid>.Fail(string.Join(" ", validation.Errors.Select(e => e.ErrorMessage)));

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var channel = await db.Channels.FirstOrDefaultAsync(c => c.Id == input.ChannelId && c.IsActive, ct);
        if (channel is null)
            return OperationResult<Guid>.Fail("Channel not found.");
        if (channel.IsLocked)
            return OperationResult<Guid>.Fail("This channel is locked and not accepting new posts.");
        if (!channel.AllowAnonymousPosts && (!currentUser.IsAuthenticated || input.PostAnonymously))
            return OperationResult<Guid>.Fail("This channel requires a signed-in account to post.");

        if (!await captcha.VerifyAsync(input.CaptchaToken, null, ct))
            return OperationResult<Guid>.Fail("Bot verification failed. Please try again.");

        var clientKey = currentUser.UserId ?? currentUser.IpHash ?? "anonymous";
        var rate = await rateLimiter.CheckAndRecordAsync(RateLimitAction.ChannelPost, clientKey, ct);
        if (!rate.Allowed)
            return OperationResult<Guid>.Fail("You have posted several times recently. Please wait before posting again.");

        var title = sanitizer.SanitizePlainText(input.Title);
        var body = sanitizer.SanitizePlainText(input.Body);

        var findings = pii.Detect(title).Findings.Concat(pii.Detect(body).Findings)
            .Select(f => $"{f.Kind}: {f.Sample}").ToList();

        var post = new ChannelPost
        {
            ChannelId = channel.Id,
            Title = title,
            Body = body,
            AuthorUserId = currentUser.IsAuthenticated && !input.PostAnonymously ? currentUser.UserId : null,
            AuthorDisplayName = currentUser.IsAuthenticated && !input.PostAnonymously ? currentUser.DisplayName : null,
            AuthorIpHash = currentUser.IpHash,
            // All posts are held for moderation in v1 (covers first-time users and anonymous posts).
            Status = ChannelPostStatus.PendingReview,
        };
        db.ChannelPosts.Add(post);

        db.ModerationCases.Add(new ModerationCase
        {
            ChannelPostId = post.Id,
            State = ModerationCaseState.Open,
            Priority = findings.Count > 0 ? ModerationPriority.High : ModerationPriority.Normal,
        });

        await db.SaveChangesAsync(ct);
        await audit.LogAsync("ChannelPost.Created", post.AuthorUserId, post.AuthorDisplayName, nameof(ChannelPost),
            post.Id.ToString(), $"Post created in {channel.Name} (held for review).", ipHash: currentUser.IpHash, ct: ct);

        return OperationResult<Guid>.Success(post.Id);
    }

    public async Task<IReadOnlyList<ChannelPostDto>> GetMyPostsAsync(string userId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.ChannelPosts.AsNoTracking()
            .Where(p => p.AuthorUserId == userId)
            .OrderByDescending(p => p.CreatedUtc)
            .Select(ToDto())
            .ToListAsync(ct);
    }

    private static System.Linq.Expressions.Expression<Func<ChannelPost, ChannelPostDto>> ToDto() => p => new ChannelPostDto
    {
        Id = p.Id,
        ChannelSlug = p.Channel!.Slug,
        ChannelName = p.Channel!.Name,
        Title = p.Title,
        Body = p.RedactedBody ?? p.Body,
        AuthorDisplayName = p.AuthorDisplayName,
        IsPinned = p.IsPinned,
        IsLocked = p.IsLocked,
        PublishedUtc = p.PublishedUtc,
    };
}
