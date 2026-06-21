using Roofied.Application.Common;
using Roofied.Domain.Enums;

namespace Roofied.Application.Flags;

public sealed class ContentFlagInput
{
    public ModeratedContentType ContentType { get; set; }
    public Guid ContentId { get; set; }
    public FlagReason Reason { get; set; } = FlagReason.Other;
    public string? Details { get; set; }
    public string? CaptchaToken { get; set; }
}

/// <summary>Lets users report a concern about a report or channel post. Rate-limited.</summary>
public interface IContentFlagService
{
    Task<OperationResult> SubmitAsync(ContentFlagInput input, CancellationToken ct = default);
}
