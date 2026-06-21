using System.Text;
using System.Text.RegularExpressions;

namespace Roofied.Infrastructure.Common;

public static partial class Slug
{
    public static string From(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return Guid.NewGuid().ToString("N")[..8];
        var lower = input.Trim().ToLowerInvariant();
        var normalized = NonAlphanumeric().Replace(lower, "-").Trim('-');
        normalized = MultiDash().Replace(normalized, "-");
        return string.IsNullOrWhiteSpace(normalized) ? Guid.NewGuid().ToString("N")[..8] : normalized;
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlphanumeric();

    [GeneratedRegex("-{2,}")]
    private static partial Regex MultiDash();
}
