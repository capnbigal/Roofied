using FluentValidation;
using Roofied.Application.Abstractions;
using Roofied.Application.Common;
using Roofied.Application.Flags;
using Roofied.Domain.Enums;
using Roofied.Domain.Moderation;
using Roofied.Infrastructure.Persistence;

namespace Roofied.Infrastructure.Services;

public sealed class ContentFlagService(
    RoofiedDbContext db,
    IValidator<ContentFlagInput> validator,
    IHtmlSanitizer sanitizer,
    IRateLimitService rateLimiter,
    ICaptchaVerifier captcha,
    ICurrentUser currentUser,
    IAuditService audit) : IContentFlagService
{
    public async Task<OperationResult> SubmitAsync(ContentFlagInput input, CancellationToken ct = default)
    {
        var validation = await validator.ValidateAsync(input, ct);
        if (!validation.IsValid)
            return OperationResult.Fail(string.Join(" ", validation.Errors.Select(e => e.ErrorMessage)));

        if (!await captcha.VerifyAsync(input.CaptchaToken, null, ct))
            return OperationResult.Fail("Bot verification failed. Please try again.");

        var clientKey = currentUser.UserId ?? currentUser.IpHash ?? "anonymous";
        var rate = await rateLimiter.CheckAndRecordAsync(RateLimitAction.ContentFlag, clientKey, ct);
        if (!rate.Allowed)
            return OperationResult.Fail("You have reported several items recently. Please try again later.");

        db.ContentFlags.Add(new ContentFlag
        {
            ContentType = input.ContentType,
            ContentId = input.ContentId,
            Reason = input.Reason,
            Details = sanitizer.SanitizePlainText(input.Details),
            FlaggedByUserId = currentUser.UserId,
            ReporterIpHash = currentUser.IpHash,
            Status = FlagStatus.Open,
        });
        await db.SaveChangesAsync(ct);

        await audit.LogAsync("Flag.Submitted", currentUser.UserId, currentUser.DisplayName,
            input.ContentType.ToString(), input.ContentId.ToString(), "Content flagged for review.",
            ipHash: currentUser.IpHash, ct: ct);

        return OperationResult.Success();
    }
}
