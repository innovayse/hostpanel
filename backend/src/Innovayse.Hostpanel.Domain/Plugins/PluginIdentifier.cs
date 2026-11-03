using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Innovayse.Hostpanel.Domain.Plugins;

/// <summary>
/// The rule a plugin id must meet. A plugin id names the plugin's own folder, a settings key prefix
/// and a URL segment, so only lower-case letters, digits and single hyphens between them are
/// accepted: nothing that a file system or a URL could read as a separator, a parent folder or a
/// drive. Two questions are kept apart: whether an id is safe to use as a folder name
/// (<see cref="IsWellFormed"/>), and whether a new plugin may take it (<see cref="IsValid"/>, which also refuses the
/// <see cref="Reserved"/> ids). A plugin installed under an id that was reserved later must stay listable and
/// removable, so listing and removal ask only the first question.
/// </summary>
public static partial class PluginIdentifier
{
    /// <summary>The longest id accepted.</summary>
    public const int MaxLength = 64;

    /// <summary>
    /// The ids no plugin may take, because the host uses them as a literal segment beside the plugin id under the plugin
    /// prefixes, so a plugin with that id would have its routes shadowed: <c>ui</c> is the panels' plugin index and pages
    /// (<c>/api/client/plugins/ui</c>) and <c>slots</c> the slot index (<c>/api/client/plugins/slots/{slotId}</c>).
    /// </summary>
    public static readonly IReadOnlySet<string> Reserved = new HashSet<string>(StringComparer.Ordinal) { "ui", "slots" };

    /// <summary>The accepted shape: lower-case alphanumeric words joined by single hyphens.</summary>
    public const string Pattern = "^[a-z0-9]+(?:-[a-z0-9]+)*$";

    /// <summary>
    /// Whether <paramref name="id"/> has the safe shape of a plugin id, reserved or not. This is the check for every place
    /// that only turns an id into a folder path (listing, removal); it never decides that a plugin may load or install.
    /// </summary>
    /// <param name="id">The candidate id, as read from a manifest, a folder name or a route.</param>
    /// <returns>
    /// <see langword="true"/> when the id is non-empty, at most <see cref="MaxLength"/> characters and matches
    /// <see cref="Pattern"/>.
    /// </returns>
    public static bool IsWellFormed([NotNullWhen(true)] string? id)
    {
        return !string.IsNullOrEmpty(id) && id.Length <= MaxLength && PatternRegex().IsMatch(id);
    }

    /// <summary>Whether <paramref name="id"/> is one of the <see cref="Reserved"/> ids.</summary>
    /// <param name="id">The candidate id.</param>
    /// <returns><see langword="true"/> when the host keeps the id for itself.</returns>
    public static bool IsReserved(string? id)
    {
        return id is not null && Reserved.Contains(id);
    }

    /// <summary>Whether <paramref name="id"/> is an id a plugin may install and load under.</summary>
    /// <param name="id">The candidate id, as read from a manifest, a folder name or a route.</param>
    /// <returns>
    /// <see langword="true"/> when the id <see cref="IsWellFormed"/> and is not <see cref="IsReserved"/>.
    /// </returns>
    public static bool IsValid([NotNullWhen(true)] string? id)
    {
        return IsWellFormed(id) && !IsReserved(id);
    }

    /// <summary>The compiled <see cref="Pattern"/>.</summary>
    /// <returns>The regular expression.</returns>
    [GeneratedRegex(Pattern, RegexOptions.CultureInvariant)]
    private static partial Regex PatternRegex();
}
