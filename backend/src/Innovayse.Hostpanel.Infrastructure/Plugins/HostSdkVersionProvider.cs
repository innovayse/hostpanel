using Innovayse.Hostpanel.Application.Common.Interfaces;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// Reports the SDK package version this host ships, read from the SDK assembly the host is compiled against.
/// </summary>
public sealed class HostSdkVersionProvider : IHostSdkVersionProvider
{
    /// <inheritdoc/>
    public string Version => HostSdkVersion.Current;
}
