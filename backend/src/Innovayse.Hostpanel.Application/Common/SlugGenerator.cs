using System.Globalization;
using System.Text;

namespace Innovayse.Hostpanel.Application.Common;

/// <summary>
/// Turns a piece of text into the slug a URL can carry: lower case Latin letters and digits joined by single
/// hyphens. It only builds the candidate and never looks at the database: making the candidate unique among
/// the slugs already taken is the job of the allocator that owns that data.
/// </summary>
public static class SlugGenerator
{
    /// <summary>Letters that do not decompose into a base letter and an accent, and what they stand for.</summary>
    private static readonly Dictionary<char, string> Specials = new()
    {
        ['ß'] = "ss",
        ['æ'] = "ae",
        ['œ'] = "oe",
        ['ø'] = "o",
        ['đ'] = "d",
        ['ł'] = "l",
    };

    /// <summary>
    /// Builds the slug candidate of a text: accents removed, everything that is not a Latin letter or a digit
    /// becomes a hyphen, runs of hyphens collapse, and the ends are trimmed.
    /// </summary>
    /// <param name="text">The text to turn into a slug, for example an article title.</param>
    /// <param name="fallback">
    /// The slug used when nothing usable is left (a title in a script this does not transliterate), and the
    /// prefix of a result that would otherwise be purely numeric. Must itself be a valid slug.
    /// </param>
    /// <param name="maxLength">The longest the result may be; the cut never leaves a trailing hyphen.</param>
    /// <returns>A non empty, lower case, never purely numeric slug candidate of at most <paramref name="maxLength"/> characters.</returns>
    public static string FromText(string? text, string fallback, int maxLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fallback);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxLength, fallback.Length + 1 + 10);

        var slug = Truncate(Build(text), maxLength);
        if (slug.Length == 0)
        {
            return fallback;
        }

        // A purely numeric slug would read as the numeric id in the same route position.
        return slug.All(char.IsAsciiDigit) ? Truncate($"{fallback}-{slug}", maxLength) : slug;
    }

    /// <summary>Builds the slug of a text without the length limit and the fallbacks.</summary>
    /// <param name="text">The text.</param>
    /// <returns>Lower case Latin letters and digits joined by single hyphens; empty when the text has none.</returns>
    private static string Build(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length);
        foreach (var raw in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(raw) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var lower = char.ToLowerInvariant(raw);
            if (Specials.TryGetValue(lower, out var expansion))
            {
                builder.Append(expansion);
            }
            else if (char.IsAsciiLetterLower(lower) || char.IsAsciiDigit(lower))
            {
                builder.Append(lower);
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        return builder.ToString().Trim('-');
    }

    /// <summary>Cuts a slug to a length without leaving a trailing hyphen.</summary>
    /// <param name="slug">The slug.</param>
    /// <param name="maxLength">The longest it may be.</param>
    /// <returns>The slug, shortened when it was longer than <paramref name="maxLength"/>.</returns>
    private static string Truncate(string slug, int maxLength) =>
        slug.Length <= maxLength ? slug : slug[..maxLength].TrimEnd('-');
}
