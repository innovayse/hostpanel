using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Innovayse.Hostpanel.Domain.Plugins;

/// <summary>
/// The rule a plugin page id must meet. A page id is a literal segment of the page address
/// (<c>/api/{area}/plugins/ui/{pluginId}/pages/{pageId}</c>) and of a panel route, so the manifest validator refuses
/// an id that breaks it and the page lookup answers 404 for one: a page the installer accepted is always reachable,
/// and a page the lookup refuses could never have been installed.
/// </summary>
public static partial class PluginPageIdentifier
{
    /// <summary>The longest page id accepted, in characters; the same bound as a plugin id.</summary>
    public const int MaxLength = PluginIdentifier.MaxLength;

    /// <summary>
    /// The accepted shape: a lowercase letter or digit, then lowercase letters, digits, hyphens and underscores.
    /// Anchored with <c>\A</c> and <c>\z</c>, never <c>^</c> and <c>$</c>, because <c>$</c> also matches before a
    /// trailing newline.
    /// </summary>
    public const string Pattern = @"\A[a-z0-9][a-z0-9_-]*\z";

    /// <summary>
    /// The page ids no plugin may declare, because the host uses them as a literal segment beside the page id:
    /// <c>by-path</c> is the page lookup by path (<c>/api/{area}/plugins/ui/{pluginId}/pages/by-path</c>), which would
    /// otherwise shadow a page with that id.
    /// </summary>
    public static readonly IReadOnlySet<string> Reserved = new HashSet<string>(StringComparer.Ordinal) { "by-path" };

    /// <summary>Whether <paramref name="id"/> is a page id a plugin may declare and a panel may ask for.</summary>
    /// <param name="id">The candidate id, as read from a manifest or a route.</param>
    /// <returns>
    /// <see langword="true"/> when the id is non-empty, at most <see cref="MaxLength"/> characters and matches
    /// <see cref="Pattern"/> and is not one of <see cref="Reserved"/>.
    /// </returns>
    public static bool IsValid([NotNullWhen(true)] string? id)
    {
        return !string.IsNullOrEmpty(id) && id.Length <= MaxLength && PatternRegex().IsMatch(id) && !Reserved.Contains(id);
    }

    /// <summary>The compiled <see cref="Pattern"/>.</summary>
    /// <returns>The regular expression.</returns>
    [GeneratedRegex(Pattern, RegexOptions.CultureInvariant)]
    private static partial Regex PatternRegex();
}
