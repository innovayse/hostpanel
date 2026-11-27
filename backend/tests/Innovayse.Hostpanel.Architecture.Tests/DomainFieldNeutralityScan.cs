using System.Text.RegularExpressions;

namespace Innovayse.Hostpanel.Architecture.Tests;

/// <summary>
/// Finds what is specific to a zone or a country in source text. The extra fields of a domain come from a registrar plugin's
/// manifest, so the host's code, tests and texts about them must hold no extension, no country code and no word that names a
/// national document or register. The rule is a plain function of named sources, so a test can run it on a planted source and
/// on the real files; it names no zone itself, it recognises their shape.
/// </summary>
internal static class DomainFieldNeutralityScan
{
    /// <summary>The only generic extensions named as plain text, which no manifest-free host has a reason to hold.</summary>
    private static readonly string[] GenericZones = ["com", "net", "org", "info", "biz"];

    /// <summary>A C# string literal that is not verbatim, raw or interpolated, which is how a zone or a name is written in code.</summary>
    private static readonly Regex Literal = new("\"((?:[^\"\\\\\\r\\n{}]|\\\\.)*)\"", RegexOptions.CultureInvariant);

    /// <summary>A literal that is a host name: dot separated labels of lower case letters, digits and hyphens.</summary>
    private static readonly Regex HostName = new("^[a-z0-9-]+(\\.[a-z0-9-]+)+$", RegexOptions.CultureInvariant);

    /// <summary>The words that name a national document or register, whatever the case, in code, comments and texts.</summary>
    private static readonly Regex Words = new(
        "\\b(passport|taxpayer|tax[ _-]?id|national[ _-]?id|identity[ _-]?card)\\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    /// <summary>What a source holds that is specific to a zone or a country.</summary>
    /// <param name="text">The source text.</param>
    /// <returns>One short description per hit, in the order found.</returns>
    public static IReadOnlyList<string> HitsIn(string text)
    {
        // A comment line quotes names of members and parameters (<c>name="id"</c>), which are no zones; only code is read for literals.
        var code = string.Join('\n', text.Split('\n').Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));
        List<string> hits = [];
        foreach (Match literal in Literal.Matches(code))
        {
            var value = literal.Groups[1].Value;
            if (IsZone(value))
            {
                hits.Add($"zone literal \"{value}\"");
            }
        }

        hits.AddRange(Words.Matches(text).Select(word => $"word '{word.Value}'"));
        return hits;
    }

    /// <summary>The sources that hold something specific to a zone or a country.</summary>
    /// <param name="sources">Pairs of a name for messages and the source text.</param>
    /// <returns>One <c>name -> hit</c> line per hit.</returns>
    public static IReadOnlyList<string> Offenders(IEnumerable<(string Name, string Text)> sources)
    {
        return sources.SelectMany(source => HitsIn(source.Text).Select(hit => $"{source.Name} -> {hit}")).ToList();
    }

    /// <summary>
    /// Tells whether a literal reads as a zone: a bare or dotted two letter label (every country extension is one), a generic
    /// extension as a whole literal or as the end of a host name, or a punycode label.
    /// </summary>
    /// <param name="value">The literal's text.</param>
    /// <returns><see langword="true"/> when the literal names a zone.</returns>
    private static bool IsZone(string value)
    {
        var bare = value.TrimStart('.');
        if (Regex.IsMatch(bare, "^[a-z]{2}$", RegexOptions.CultureInvariant) || GenericZones.Contains(bare))
        {
            return true;
        }

        if (value.Contains("xn--", StringComparison.Ordinal))
        {
            return true;
        }

        if (!HostName.IsMatch(value))
        {
            return false;
        }

        var last = value[(value.LastIndexOf('.') + 1)..];
        return last.Length == 2 || GenericZones.Contains(last);
    }
}
