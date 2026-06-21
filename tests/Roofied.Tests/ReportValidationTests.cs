using Roofied.Application.Reports;
using Roofied.Application.Reports.Validation;
using Roofied.Domain.Enums;

namespace Roofied.Tests;

public class ReportValidationTests
{
    private readonly ReportSubmissionInputValidator _validator = new();

    private static ReportSubmissionInput Valid() => new()
    {
        City = "Springfield",
        ReportCategoryId = Guid.NewGuid(),
        IncidentDate = new DateOnly(2026, 1, 1),
        SuspicionLevel = SuspicionLevel.Suspected,
        SafetyNoticeAcknowledged = true,
        ConsentAcknowledged = true,
    };

    [Fact]
    public void Valid_input_passes() => Assert.True(_validator.Validate(Valid()).IsValid);

    [Fact]
    public void City_is_required()
    {
        var input = Valid();
        input.City = "";
        Assert.False(_validator.Validate(input).IsValid);
    }

    [Fact]
    public void Category_is_required()
    {
        var input = Valid();
        input.ReportCategoryId = null;
        Assert.False(_validator.Validate(input).IsValid);
    }

    [Fact]
    public void Consent_and_safety_acknowledgement_are_required()
    {
        var noConsent = Valid();
        noConsent.ConsentAcknowledged = false;
        Assert.False(_validator.Validate(noConsent).IsValid);

        var noSafety = Valid();
        noSafety.SafetyNoticeAcknowledged = false;
        Assert.False(_validator.Validate(noSafety).IsValid);
    }

    [Fact]
    public void Future_incident_date_is_rejected()
    {
        var input = Valid();
        input.IncidentDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));
        Assert.False(_validator.Validate(input).IsValid);
    }

    [Fact]
    public void Partial_coordinates_are_rejected()
    {
        var input = Valid();
        input.Latitude = 40.0;
        input.Longitude = null;
        Assert.False(_validator.Validate(input).IsValid);
    }
}
