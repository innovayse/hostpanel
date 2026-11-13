using Hostpanel.Sdk.Packaging.Enums;
using Hostpanel.Sdk.Packaging.Naming;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common;

/// <summary>The one answer to "is the registry's version an update", shared by the registry browser and the update check.</summary>
public static class PluginUpdateRule
{
    /// <summary>Tells whether the registry's newest version should be offered as an update.</summary>
    /// <param name="installed">The installed version text.</param>
    /// <param name="staged">The version of an upload already waiting for the restart; <see langword="null"/> when none.</param>
    /// <param name="latest">The registry's newest version text.</param>
    /// <returns>
    /// <see langword="true"/> only when <paramref name="latest"/> is a newer semantic version than the installed one and
    /// than the staged one; a version that does not parse is never offered, because it cannot be proven newer.
    /// </returns>
    public static bool IsUpdate(string installed, string? staged, string latest)
    {
        if (PluginVersion.Compare(installed, latest) != PluginVersionChange.Newer)
        {
            return false;
        }

        return staged is null || PluginVersion.Compare(staged, latest) == PluginVersionChange.Newer;
    }

    /// <summary>
    /// Picks the version to offer as an update: the newest published version that is an update and whose host range admits
    /// the running host. An update the host cannot run is never offered, even when it is the registry's newest version.
    /// </summary>
    /// <param name="versions">Every version the registry lists for the plugin.</param>
    /// <param name="hostVersion">The running Hostpanel version text.</param>
    /// <param name="installed">The installed version text.</param>
    /// <param name="staged">The version of an upload already waiting for the restart; <see langword="null"/> when none.</param>
    /// <returns>
    /// The version to offer; <see langword="null"/> when none qualifies. A version listed more than once is judged by its
    /// strictest row, and only a <see cref="PluginRegistryVersionStatus.Published"/> version qualifies.
    /// </returns>
    public static RegistryPluginVersion? NewestOffer(
        IEnumerable<RegistryPluginVersion> versions, string hostVersion, string installed, string? staged)
    {
        return versions
            .GroupBy(v => v.Version, StringComparer.Ordinal)
            .Select(g => g.OrderByDescending(v => v.Status.Strictness()).First())
            .Where(v => v.Status == PluginRegistryVersionStatus.Published
                && PluginHostCompatibility.IsCompatible(hostVersion, v.MinHostVersion, v.MaxHostVersion)
                && IsUpdate(installed, staged, v.Version))
            .OrderByDescending(v => v.Version, Comparer<string>.Create(CompareVersions))
            .FirstOrDefault();
    }

    /// <summary>Orders two version texts that both parse as versions (the caller has already kept only updates, which parse).</summary>
    /// <param name="left">The first version text.</param>
    /// <param name="right">The second version text.</param>
    /// <returns>A negative number when <paramref name="left"/> is older, positive when newer, zero when equal.</returns>
    private static int CompareVersions(string left, string right)
    {
        return PluginVersion.Compare(right, left) switch
        {
            PluginVersionChange.Newer => 1,
            PluginVersionChange.Older => -1,
            _ => 0,
        };
    }
}
