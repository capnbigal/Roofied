using System.Text.RegularExpressions;

namespace Roofied.Application.Safety;

/// <summary>
/// Minimal, replaceable profanity filter. The default word list is intentionally small and
/// conservative; it is meant as a first-pass screen, not a content-policy engine. Survivor
/// narratives are never auto-rejected on profanity — flagged content goes to moderators.
/// </summary>
public interface IProfanityFilter
{
    bool ContainsProfanity(string? text);
    string Mask(string? text);
}

public sealed class ProfanityFilter : IProfanityFilter
{
    // Deliberately short default set. Extend via configuration in production if desired.
    private static readonly string[] DefaultWords =
    {
        "fuck", "shit", "bitch", "asshole", "bastard", "cunt", "dick", "piss",
    };

    private readonly Regex _regex;

    public ProfanityFilter(IEnumerable<string>? words = null)
    {
        var list = (words ?? DefaultWords).Where(w => !string.IsNullOrWhiteSpace(w)).Select(Regex.Escape);
        var pattern = @"\b(" + string.Join("|", list) + @")\b";
        _regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }

    public bool ContainsProfanity(string? text) =>
        !string.IsNullOrWhiteSpace(text) && _regex.IsMatch(text);

    public string Mask(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;
        return _regex.Replace(text, m => new string('*', m.Value.Length));
    }
}
