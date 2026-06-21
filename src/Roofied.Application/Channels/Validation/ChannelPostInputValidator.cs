using FluentValidation;

namespace Roofied.Application.Channels.Validation;

public sealed class ChannelPostInputValidator : AbstractValidator<ChannelPostInput>
{
    public ChannelPostInputValidator()
    {
        RuleFor(x => x.ChannelId).NotEmpty();
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Please add a title.")
            .MaximumLength(160);
        RuleFor(x => x.Body)
            .NotEmpty().WithMessage("Please write your post.")
            .MaximumLength(8000);
        RuleFor(x => x.GuidelinesAcknowledged)
            .Equal(true).WithMessage("Please confirm you have read the posting guidelines.");
    }
}
