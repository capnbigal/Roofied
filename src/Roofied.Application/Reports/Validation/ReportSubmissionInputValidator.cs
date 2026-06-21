using FluentValidation;

namespace Roofied.Application.Reports.Validation;

public sealed class ReportSubmissionInputValidator : AbstractValidator<ReportSubmissionInput>
{
    public ReportSubmissionInputValidator()
    {
        RuleFor(x => x.City)
            .NotEmpty().WithMessage("Please provide a city or general area.")
            .MaximumLength(120);

        RuleFor(x => x.Region).MaximumLength(120);
        RuleFor(x => x.Country).MaximumLength(120);

        RuleFor(x => x.ReportCategoryId)
            .NotNull().WithMessage("Please choose an incident type.");

        RuleFor(x => x.IncidentDate)
            .NotNull().WithMessage("Please provide the incident date.")
            .Must(d => d is null || d.Value <= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)))
            .WithMessage("The incident date cannot be in the future.");

        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90, 90).When(x => x.Latitude.HasValue);
        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180, 180).When(x => x.Longitude.HasValue);

        // If one coordinate is supplied, both must be.
        RuleFor(x => x).Must(x =>
                x.Latitude.HasValue == x.Longitude.HasValue)
            .WithMessage("Both latitude and longitude are required to place a map point.");

        RuleFor(x => x.Narrative)
            .MaximumLength(8000);
        RuleFor(x => x.Symptoms)
            .MaximumLength(4000);

        RuleFor(x => x.PrivateContactValue)
            .MaximumLength(256);
        RuleFor(x => x.PrivateContactMethod)
            .MaximumLength(64);

        RuleFor(x => x.SafetyNoticeAcknowledged)
            .Equal(true).WithMessage("Please acknowledge the safety notice.");

        RuleFor(x => x.ConsentAcknowledged)
            .Equal(true).WithMessage("Please confirm you understand how this report will be used.");
    }
}
