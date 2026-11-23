namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// Returns the version of the <c>Hostpanel.Sdk</c> package the running Hostpanel build ships, the one its own plugin
/// contracts are compiled against. It decides which plugins the host can load.
/// </summary>
public interface IHostSdkVersionProvider
{
    /// <summary>Gets the SDK package version as <c>major.minor.patch</c>; empty when the build carries none.</summary>
    public string Version { get; }
}
