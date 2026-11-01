using Innovayse.SDK.Plugins.HostingPanel;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// A configurable <see cref="IHostingPanelApiClient"/> standing in for the real hosting panel control-panel API a
/// test must not call over the network. Succeeds with fixed values by default; setting
/// <see cref="ShouldThrow"/> makes it throw, the way an unreachable panel would.
/// </summary>
public sealed class FakeHostingPanelApiClient : IHostingPanelApiClient
{
    /// <summary>When <see langword="true"/>, <see cref="GetServerInfoAsync"/> throws instead of answering.</summary>
    public bool ShouldThrow { get; set; }

    /// <summary>The account count returned when <see cref="ShouldThrow"/> is <see langword="false"/>.</summary>
    public int AccountsCount { get; set; } = 5;

    /// <summary>The version string returned when <see cref="ShouldThrow"/> is <see langword="false"/>.</summary>
    public string Version { get; set; } = "1.9.1";

    /// <summary>The connections this fake was asked to probe, in call order.</summary>
    public List<HostingPanelConnection> Connections { get; } = [];

    /// <inheritdoc/>
    public Task<HostingPanelServerInfo> GetServerInfoAsync(HostingPanelConnection connection, CancellationToken ct)
    {
        Connections.Add(connection);

        if (ShouldThrow)
        {
            throw new HttpRequestException(
                "fake hosting panel server refused the request with connection details nobody should see");
        }

        return Task.FromResult(new HostingPanelServerInfo(AccountsCount, Version));
    }
}
