using Innovayse.SDK.Plugins.HostingPanel;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// A configurable <see cref="IHostingPanelV7ApiClient"/> standing in for the real hosting panel (v7) control-panel API a
/// test must not call over the network. Succeeds with a fixed value by default; setting
/// <see cref="ShouldThrow"/> makes it throw, the way an unreachable panel would.
/// </summary>
public sealed class FakeHostingPanelV7ApiClient : IHostingPanelV7ApiClient
{
    /// <summary>When <see langword="true"/>, <see cref="GetServerInfoAsync"/> throws instead of answering.</summary>
    public bool ShouldThrow { get; set; }

    /// <summary>The package count returned when <see cref="ShouldThrow"/> is <see langword="false"/>.</summary>
    public int PackagesCount { get; set; } = 7;

    /// <summary>The connections this fake was asked to probe, in call order.</summary>
    public List<HostingPanelConnection> Connections { get; } = [];

    /// <inheritdoc/>
    public Task<HostingPanelV7ServerInfo> GetServerInfoAsync(HostingPanelConnection connection, CancellationToken ct)
    {
        Connections.Add(connection);

        if (ShouldThrow)
        {
            throw new HttpRequestException(
                "fake hosting panel server refused the request with connection details nobody should see");
        }

        return Task.FromResult(new HostingPanelV7ServerInfo(PackagesCount));
    }
}
