using Hostpanel.Sdk.Packaging.Enums;
using Hostpanel.Sdk.Packaging.Naming;

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
}
