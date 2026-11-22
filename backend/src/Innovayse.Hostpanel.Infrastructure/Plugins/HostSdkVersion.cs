using Hostpanel.Sdk.Plugins.Manifest;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// The version of the <c>Hostpanel.Sdk</c> package this host ships, read once from the SDK assembly the host's own
/// plugin contracts come from. It is the single source for <see cref="HostSdkVersionProvider"/> and for the manifest
/// validation, which runs before the container exists.
/// </summary>
internal static class HostSdkVersion
{
    /// <summary>Gets the SDK package version as <c>major.minor.patch</c>, the form the build tooling writes into a manifest.</summary>
    public static string Current { get; } = Read();

    /// <summary>Reads the version of the SDK assembly.</summary>
    /// <returns>The version as <c>major.minor.patch</c>; empty when the assembly carries none.</returns>
    private static string Read()
    {
        var version = typeof(PluginManifest).Assembly.GetName().Version;
        return version is null ? string.Empty : $"{version.Major}.{version.Minor}.{Math.Max(version.Build, 0)}";
    }
}
