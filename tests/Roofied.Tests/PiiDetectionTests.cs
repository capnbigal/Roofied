using Roofied.Application.Safety;

namespace Roofied.Tests;

public class PiiDetectionTests
{
    private readonly PiiDetectionService _svc = new();

    [Fact]
    public void Detects_email_phone_url_and_handle()
    {
        var result = _svc.Detect("Contact me at jane.doe@example.com or 555-123-4567, see http://x.test or @janedoe");
        Assert.Contains(result.Findings, f => f.Kind == PiiKind.Email);
        Assert.Contains(result.Findings, f => f.Kind == PiiKind.PhoneNumber);
        Assert.Contains(result.Findings, f => f.Kind == PiiKind.Url);
        Assert.Contains(result.Findings, f => f.Kind == PiiKind.SocialHandle);
    }

    [Fact]
    public void Detects_possible_accusation_phrases()
    {
        var result = _svc.Detect("The bartender did it, his name is John");
        Assert.True(result.HasFindings);
        Assert.Contains(result.Findings, f => f.Kind == PiiKind.PossibleAccusation);
    }

    [Fact]
    public void Detects_street_address()
    {
        var result = _svc.Detect("It happened near 123 Main Street downtown");
        Assert.Contains(result.Findings, f => f.Kind == PiiKind.StreetAddress);
    }

    [Fact]
    public void Clean_text_has_no_findings()
    {
        var result = _svc.Detect("I felt unwell after one drink and went home with a friend.");
        Assert.False(result.HasFindings);
    }

    [Fact]
    public void Null_or_empty_is_safe()
    {
        Assert.False(_svc.Detect(null).HasFindings);
        Assert.False(_svc.Detect("   ").HasFindings);
    }
}
