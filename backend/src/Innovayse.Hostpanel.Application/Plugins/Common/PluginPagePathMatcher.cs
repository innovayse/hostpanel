using Innovayse.SDK.Plugins.Manifest;

/*
 * A page path arrives from a panel's address bar, so it is untrusted text. The manifest validator holds a page path to
 * the route path rule (a leading '/', lowercase literal or {parameter} segments, no empty, '.' or '..' segment), and
 * the matching follows PluginRouteMatcher: literals compare ignoring case, a parameter takes one non-empty segment, a
 * literal beats a parameter. PluginRouteMatcher itself is not reused because it binds a method and route values a page
 * has none of; what is shared is the template rule, which is the manifest's.
 */

namespace Innovayse.Hostpanel.Application.Plugins.Common;

/// <summary>
/// Finds the page a requested panel path names among the pages one area of a manifest declares. A path that breaks the
/// shape rule below names no page, which the caller answers exactly like an unknown page.
/// </summary>
public static class PluginPagePathMatcher
{
    /// <summary>
    /// The longest requested path accepted, in characters. A page path is a few segments below the plugin's own prefix;
    /// the bound only keeps an absurd query string from being split and compared against every page.
    /// </summary>
    public const int MaxPathLength = 512;

    /// <summary>The character that starts a path and separates its segments.</summary>
    private const char Separator = '/';

    /// <summary>A backslash, never part of a page path.</summary>
    private const char Backslash = '\\';

    /// <summary>The character a manifest's <c>{parameter}</c> segment starts with.</summary>
    private const char ParameterStart = '{';

    /// <summary>The segment that names the current folder, never a page segment.</summary>
    private const string CurrentSegment = ".";

    /// <summary>The segment that names the parent folder, never a page segment.</summary>
    private const string ParentSegment = "..";

    /// <summary>Splits a requested path into its segments when it has the shape a page path can have.</summary>
    /// <param name="path">The path from the query string, decoded once by the server.</param>
    /// <returns>
    /// The segments without the leading <c>/</c>, or <see langword="null"/> when the path is missing, over
    /// <see cref="MaxPathLength"/>, does not start with <c>/</c>, or holds an empty, <c>.</c> or <c>..</c> segment, a
    /// backslash or a control character.
    /// </returns>
    public static string[]? Segments(string? path)
    {
        if (string.IsNullOrEmpty(path) || path.Length > MaxPathLength || path[0] != Separator)
        {
            return null;
        }

        // A backslash is a separator to some clients and a control character can split a log line; neither is ever
        // part of a page path, so either refuses the whole path rather than being compared as text.
        foreach (var character in path)
        {
            if (character == Backslash || char.IsControl(character))
            {
                return null;
            }
        }

        var segments = path[1..].Split(Separator);
        foreach (var segment in segments)
        {
            if (segment.Length == 0 || segment == CurrentSegment || segment == ParentSegment)
            {
                return null;
            }
        }

        return segments;
    }

    /// <summary>Finds the declared page whose path template the segments fit.</summary>
    /// <param name="pages">The pages of one area of the manifest, in manifest order.</param>
    /// <param name="segments">The requested path's segments, from <see cref="Segments"/>.</param>
    /// <returns>
    /// The page with the most literal segments among those of the same segment count whose literals equal the
    /// request's, ignoring case, the first in manifest order on a tie; <see langword="null"/> when none fits.
    /// </returns>
    public static PluginPage? Match(IReadOnlyList<PluginPage> pages, IReadOnlyList<string> segments)
    {
        PluginPage? best = null;
        var bestLiterals = -1;

        foreach (var page in pages)
        {
            var literals = LiteralsMatched(page.Path, segments);

            // Strictly greater, so a later page with the same template never replaces the first one declared.
            if (literals > bestLiterals)
            {
                best = page;
                bestLiterals = literals;
            }
        }

        return best;
    }

    /// <summary>Fits the segments to one page path template.</summary>
    /// <param name="template">The declared page path, such as <c>/sites/{siteId}</c>.</param>
    /// <param name="segments">The requested path's segments.</param>
    /// <returns>
    /// The number of literal segments that matched, or <c>-1</c> when the segments do not fit the template.
    /// </returns>
    private static int LiteralsMatched(string template, IReadOnlyList<string> segments)
    {
        var declared = template.TrimStart(Separator).Split(Separator);
        if (declared.Length != segments.Count)
        {
            return -1;
        }

        var literals = 0;
        for (var i = 0; i < declared.Length; i++)
        {
            if (declared[i].StartsWith(ParameterStart))
            {
                // Segments() already refused an empty segment, so any segment fills a parameter.
                continue;
            }

            if (!string.Equals(declared[i], segments[i], StringComparison.OrdinalIgnoreCase))
            {
                return -1;
            }

            literals++;
        }

        return literals;
    }
}
