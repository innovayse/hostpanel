using Hostpanel.Sdk.Packaging.Naming;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common;

/// <summary>The one answer to "does this plugin version run on the Hostpanel version this server runs", shared by the details page and the install.</summary>
public static class PluginHostCompatibility
{
    /// <summary>Tells whether a version's host range admits the running host.</summary>
    /// <param name="hostVersion">The running Hostpanel version text; <c>dev</c> or any text that is not a version for a local build.</param>
    /// <param name="minHostVersion">The oldest host version the plugin version runs on; <see langword="null"/> or blank for no lower bound.</param>
    /// <param name="maxHostVersion">The newest host version the plugin version runs on; <see langword="null"/> or blank for no upper bound.</param>
    /// <returns>
    /// <see langword="true"/> when the host is inside the range. A host that is not a version (a local build) cannot be placed
    /// against a range and is not refused; a bound that is not a version fails closed, because the range cannot be proven to admit the host.
    /// </returns>
    public static bool IsCompatible(string hostVersion, string? minHostVersion, string? maxHostVersion)
    {
        var hasMin = !string.IsNullOrWhiteSpace(minHostVersion);
        var hasMax = !string.IsNullOrWhiteSpace(maxHostVersion);
        if (!hasMin && !hasMax)
        {
            return true;
        }

        PluginVersion? min = null;
        if (hasMin && !PluginVersion.TryParse(minHostVersion, out min))
        {
            return false;
        }

        PluginVersion? max = null;
        if (hasMax && !PluginVersion.TryParse(maxHostVersion, out max))
        {
            return false;
        }

        if (!PluginVersion.TryParse(hostVersion, out var host))
        {
            return true;
        }

        return (min is null || host.CompareTo(min) >= 0) && (max is null || host.CompareTo(max) <= 0);
    }
}
