using System.Text.RegularExpressions;

namespace Innovayse.Hostpanel.Architecture.Tests;

/// <summary>
/// Finds the places that read the system clock directly: <c>DateTimeOffset.UtcNow</c>, <c>DateTimeOffset.Now</c>,
/// <c>DateTime.UtcNow</c>, <c>DateTime.Now</c> and <c>DateTime.Today</c>. The rule is a plain function of named sources, so a test
/// can run it on an in-memory planted source and on the real files.
/// </summary>
internal static partial class DirectClockScan
{
    /// <summary>A direct read of the system clock, with the type named in full or through its namespace.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"\b(?:DateTimeOffset|DateTime)\s*\.\s*(?:UtcNow|Now|Today)\b", RegexOptions.CultureInvariant)]
    private static partial Regex ClockRead();

    /// <summary>A block comment, a line comment or a documentation comment, none of which reads the clock.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"/\*.*?\*/|(?<!:)//[^\r\n]*", RegexOptions.CultureInvariant | RegexOptions.Singleline)]
    private static partial Regex Comment();

    /// <summary>The sources that read the system clock outside a comment.</summary>
    /// <param name="sources">Pairs of a name for messages and the source text.</param>
    /// <returns>One <c>name: DateTimeOffset.UtcNow</c> line per read.</returns>
    public static IReadOnlyList<string> Offenders(IEnumerable<(string Name, string Text)> sources)
    {
        return sources
            .SelectMany(source => ClockRead().Matches(Comment().Replace(source.Text, " "))
                .Select(match => $"{source.Name}: {Regex.Replace(match.Value, @"\s+", string.Empty)}"))
            .ToList();
    }
}
