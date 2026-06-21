using System.Text.RegularExpressions;

namespace Roofied.Application.Safety;

public enum PiiKind
{
    Email,
    PhoneNumber,
    Url,
    SocialHandle,
    StreetAddress,
    PossibleVehicle,
    PossibleAccusation,
}

public sealed record PiiFinding(PiiKind Kind, string Sample);

public sealed record PiiDetectionResult(IReadOnlyList<PiiFinding> Findings)
{
    public bool HasFindings => Findings.Count > 0;
    public static readonly PiiDetectionResult Empty = new(Array.Empty<PiiFinding>());
}

/// <summary>
/// Heuristic detector for personal information and accusations against identifiable people/venues.
/// It is intentionally conservative and used only to FLAG content for moderator review — it never
/// auto-publishes or auto-rejects. Moderators make the final redaction decision.
/// </summary>
public interface IPiiDetectionService
{
    PiiDetectionResult Detect(string? text);
}

public sealed partial class PiiDetectionService : IPiiDetectionService
{
    public PiiDetectionResult Detect(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return PiiDetectionResult.Empty;

        var findings = new List<PiiFinding>();

        AddMatches(findings, PiiKind.Email, EmailRegex(), text);
        AddMatches(findings, PiiKind.Url, UrlRegex(), text);
        AddMatches(findings, PiiKind.SocialHandle, SocialHandleRegex(), text);
        AddMatches(findings, PiiKind.PhoneNumber, PhoneRegex(), text);
        AddMatches(findings, PiiKind.StreetAddress, StreetAddressRegex(), text);
        AddMatches(findings, PiiKind.PossibleVehicle, VehicleRegex(), text);
        AddMatches(findings, PiiKind.PossibleAccusation, AccusationRegex(), text);

        return findings.Count == 0 ? PiiDetectionResult.Empty : new PiiDetectionResult(findings);
    }

    private static void AddMatches(List<PiiFinding> findings, PiiKind kind, Regex regex, string text)
    {
        foreach (Match m in regex.Matches(text))
        {
            if (!m.Success || string.IsNullOrWhiteSpace(m.Value))
                continue;
            var sample = m.Value.Trim();
            if (sample.Length > 120)
                sample = sample[..120];
            findings.Add(new PiiFinding(kind, sample));
            if (findings.Count >= 50)
                return; // safety cap
        }
    }

    [GeneratedRegex(@"[\w.+-]+@[\w-]+\.[\w.-]+", RegexOptions.IgnoreCase)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"(https?://|www\.)\S+", RegexOptions.IgnoreCase)]
    private static partial Regex UrlRegex();

    [GeneratedRegex(@"(?<![\w./])@[A-Za-z0-9_]{2,30}")]
    private static partial Regex SocialHandleRegex();

    // 7+ digits allowing common separators; flags potential phone numbers.
    [GeneratedRegex(@"(?<!\d)(\+?\d[\d\s().-]{6,}\d)(?!\d)")]
    private static partial Regex PhoneRegex();

    [GeneratedRegex(@"\b\d{1,5}\s+([A-Za-z0-9.''-]+\s+){0,3}(street|st|avenue|ave|road|rd|boulevard|blvd|lane|ln|drive|dr|court|ct|place|pl|way|terrace|ter)\b", RegexOptions.IgnoreCase)]
    private static partial Regex StreetAddressRegex();

    [GeneratedRegex(@"\b(license|licence|number)\s*plate\b|\bplate\s*(number|no|#)\b|\b[A-Z]{2,3}[-\s]?\d{3,4}\b", RegexOptions.IgnoreCase)]
    private static partial Regex VehicleRegex();

    // Phrases that often precede an accusation against an identifiable person/venue.
    [GeneratedRegex(@"\b(his name (is|was)|her name (is|was)|their name (is|was)|named|called)\s+[A-Z][a-z]+|\b(the (bartender|waiter|waitress|server|bouncer|manager|owner|driver|dj))\b", RegexOptions.IgnoreCase)]
    private static partial Regex AccusationRegex();
}
