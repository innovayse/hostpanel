using Hostpanel.Sdk.Packaging.Naming;
using Hostpanel.Sdk.Plugins.Manifest;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common;

/// <summary>
/// The one answer to "can this host load a plugin built against that SDK version", shared by the upload and install checks,
/// the loader, the update check and the registry browser. The host knows which <c>Hostpanel.Sdk</c> it ships because the
/// assembly is the one its own code is compiled against; a plugin built on a newer SDK fails at the next start with a load
/// error that names nothing useful, so it is refused up front.
/// </summary>
public static class PluginSdkCompatibility
{
    /// <summary>Gets the version of the <c>Hostpanel.Sdk</c> package this host ships, as <c>major.minor.patch</c>.</summary>
    public static string HostSdkVersion { get; } = ReadHostSdkVersion();

    /// <summary>Tells whether the host this code runs on can load a plugin built against an SDK version.</summary>
    /// <param name="pluginSdkVersion">The plugin's <c>sdkPackageVersion</c>, or the registry's <c>sdkVersion</c> for the version.</param>
    /// <returns>See <see cref="IsSupported(string, string?)"/>.</returns>
    public static bool IsSupported(string? pluginSdkVersion)
    {
        return IsSupported(HostSdkVersion, pluginSdkVersion);
    }

    /// <summary>Tells whether a host that ships one SDK version can load a plugin built against another.</summary>
    /// <param name="hostSdkVersion">The SDK version the host ships.</param>
    /// <param name="pluginSdkVersion">The SDK version the plugin was built against; <see langword="null"/> or blank when not stated.</param>
    /// <returns>
    /// <see langword="true"/> when the plugin states no SDK version (a plugin built before the build tooling wrote one is not
    /// checked), or when its version is not newer than the host's. A stated value that is not a version fails closed, because
    /// it cannot be proven loadable; a host version that is not a version cannot be compared and refuses nothing.
    /// </returns>
    public static bool IsSupported(string hostSdkVersion, string? pluginSdkVersion)
    {
        if (string.IsNullOrWhiteSpace(pluginSdkVersion))
        {
            return true;
        }

        if (!PluginVersion.TryParse(pluginSdkVersion, out var plugin))
        {
            return false;
        }

        return !PluginVersion.TryParse(hostSdkVersion, out var host) || plugin.CompareTo(host) <= 0;
    }

    /// <summary>Reads the version of the SDK assembly the host's own plugin contracts come from.</summary>
    /// <returns>The version as <c>major.minor.patch</c>, the same form the build tooling writes into a plugin's manifest.</returns>
    private static string ReadHostSdkVersion()
    {
        var version = typeof(PluginManifest).Assembly.GetName().Version!;
        return $"{version.Major}.{version.Minor}.{Math.Max(version.Build, 0)}";
    }
}
