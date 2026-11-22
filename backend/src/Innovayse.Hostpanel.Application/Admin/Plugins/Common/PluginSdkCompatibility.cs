using Hostpanel.Sdk.Packaging.Naming;
using Innovayse.Hostpanel.Application.Common.Interfaces;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common;

/// <summary>
/// The one answer to "can this host load a plugin built against that SDK version", shared by the upload and install
/// checks, the loader, the update check and the registry browser. A plugin built on a newer SDK fails at the next
/// start with a load error that names nothing useful, so it is refused up front. The host's own SDK version comes
/// from <see cref="IHostSdkVersionProvider"/>.
/// </summary>
public static class PluginSdkCompatibility
{
    /// <summary>Tells whether a host that ships one SDK version can load a plugin built against another.</summary>
    /// <param name="hostSdkVersion">The SDK version the host ships.</param>
    /// <param name="pluginSdkVersion">
    /// The SDK version the plugin was built against; <see langword="null"/> or blank when not stated.
    /// </param>
    /// <returns>
    /// <see langword="false"/> only when the plugin's version is a version and is newer than the host's. A version that
    /// is not stated, or is not a version (a manifest's is refused by the package checker, a registry's is only its
    /// claim), is not checked; a host version that is not a version cannot be compared and refuses nothing.
    /// </returns>
    public static bool IsSupported(string hostSdkVersion, string? pluginSdkVersion)
    {
        if (!PluginVersion.TryParse(pluginSdkVersion, out var plugin) || !PluginVersion.TryParse(hostSdkVersion, out var host))
        {
            return true;
        }

        return plugin.CompareTo(host) <= 0;
    }
}
