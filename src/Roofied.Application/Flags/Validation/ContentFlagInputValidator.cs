using FluentValidation;

namespace Roofied.Application.Flags.Validation;

public sealed class ContentFlagInputValidator : AbstractValidator<ContentFlagInput>
{
    public ContentFlagInputValidator()
    {
        RuleFor(x => x.ContentId).NotEmpty();
        RuleFor(x => x.Details).MaximumLength(2000);
    }
}
