using Hostpanel.Sdk.Plugins.HostingPanel;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Servers;
using Innovayse.Hostpanel.Infrastructure.Provisioning;
using Innovayse.Hostpanel.Infrastructure.Provisioning.Settings;
using Innovayse.Hostpanel.Integration.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Integration.Tests.Provisioning;

/// <summary>
/// <see cref="ServerConnectionTester"/> for a stored server on the legacy CentOS module, which no plugin serves: the host's own
/// panel client is asked, the stored credential picks the API key, and a failure never leaks the provider's text.
/// </summary>
public sealed class ServerConnectionTesterLegacyCentosTests
{
    /// <summary>The access hash of the server, used as the API key.</summary>
    private const string AccessHash = "stored-access-hash";

    /// <summary>The API token of the server, used as the API key when there is no access hash.</summary>
    private const string ApiToken = "stored-api-token";

    /// <summary>A reachable server answers a successful result with the version and account count the panel reported.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task TestAsync_Reachable_CarriesTheVersionAndTheAccountCountAsync()
    {
        var panel = new FakeHostingPanelApiClient { AccountsCount = 12, Version = "0.9.8" };
        await using var host = BuildHost(panel);

        var result = await host.Tester.TestAsync(CentosServer(new ServerCredentials(null, null, AccessHash)), CancellationToken.None);

        result.Connected.Should().BeTrue();
        result.AccountsCount.Should().Be(12);
        result.Version.Should().Be("0.9.8");
        result.ErrorCode.Should().BeNull();
    }

    /// <summary>The access hash is the key the panel is asked with, on the panel's default API port.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task TestAsync_AccessHash_IsTheKeyOnTheDefaultPortAsync()
    {
        var panel = new FakeHostingPanelApiClient();
        await using var host = BuildHost(panel);

        await host.Tester.TestAsync(CentosServer(new ServerCredentials(null, ApiToken, AccessHash)), CancellationToken.None);

        var connection = panel.Connections.Should().ContainSingle().Which;
        connection.ApiKey.Should().Be(AccessHash);
        connection.Host.Should().Be("centos.example.test");
#pragma warning disable CS0618 // Legacy HostingPanel contract, obsolete since SDK 3.1.0; the tester still uses its default port.
        connection.Port.Should().Be(IHostingPanelApiClient.DefaultApiPort);
#pragma warning restore CS0618
    }

    /// <summary>A server with an API address is asked at that address, not at its host name.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task TestAsync_WithAnApiHost_AsksThePanelAtThatAddressAsync()
    {
        var panel = new FakeHostingPanelApiClient();
        await using var host = BuildHost(panel);

        await host.Tester.TestAsync(CentosServer(new ServerCredentials(null, null, AccessHash), apiHost: "10.77.0.1"), CancellationToken.None);

        panel.Connections.Should().ContainSingle().Which.Host.Should().Be("10.77.0.1");
    }

    /// <summary>A server without an access hash falls back to its API token.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task TestAsync_WithoutAccessHash_UsesTheApiTokenAsync()
    {
        var panel = new FakeHostingPanelApiClient();
        await using var host = BuildHost(panel);

        var result = await host.Tester.TestAsync(CentosServer(new ServerCredentials(null, ApiToken, null)), CancellationToken.None);

        result.Connected.Should().BeTrue();
        panel.Connections.Should().ContainSingle().Which.ApiKey.Should().Be(ApiToken);
    }

    /// <summary>A server with no API key fails with its own code and never reaches the panel.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task TestAsync_NoApiKey_FailsWithoutCallingThePanelAsync()
    {
        var panel = new FakeHostingPanelApiClient();
        await using var host = BuildHost(panel);

        var result = await host.Tester.TestAsync(CentosServer(ServerCredentials.None), CancellationToken.None);

        result.Connected.Should().BeFalse();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.SERVER_CONNECTION_NO_API_KEY);
        panel.Connections.Should().BeEmpty();
    }

    /// <summary>A panel that fails is an unreachable server; the cause is logged and its text never reaches the result.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task TestAsync_PanelThrows_IsUnreachableAndLogsTheCauseAsync()
    {
        var panel = new FakeHostingPanelApiClient { ShouldThrow = true };
        await using var host = BuildHost(panel);

        var result = await host.Tester.TestAsync(CentosServer(new ServerCredentials(null, null, AccessHash)), CancellationToken.None);

        result.Connected.Should().BeFalse();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.SERVER_CONNECTION_UNREACHABLE);
        var lines = host.Logs.Entries.Where(e => e.Message.StartsWith("CWP connection test failed", StringComparison.Ordinal)).ToList();
        lines.Should().ContainSingle();
        lines[0].Exception.Should().BeOfType<HttpRequestException>();
    }

    /// <summary>Builds a stored server on the legacy CentOS module.</summary>
    /// <param name="credentials">The credentials the server stores.</param>
    /// <param name="apiHost">The address the server's API is dialled at, or <see langword="null"/> for its host name.</param>
    /// <returns>The server.</returns>
    private static Server CentosServer(ServerCredentials credentials, string? apiHost = null) =>
        Server.Create(
            new ServerDetails(
                "centos", "centos.example.test", null, null, ServerModules.Centos, "root",
                credentials, true, null, false, false, 0m, null, null,
                ServerNameServers.None,
                false,
                apiHost),
            TestMoments.Seeded);

    /// <summary>Builds a tester that asks the given panel client and has no plugin serving any module.</summary>
    /// <param name="panel">The fake panel client.</param>
    /// <returns>The tester with the log it writes to.</returns>
    private static TesterHost BuildHost(FakeHostingPanelApiClient panel)
    {
        var logs = new RecordingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(logs));
        services.AddHttpClient();
        var provider = services.BuildServiceProvider();
#pragma warning disable CS0618 // Legacy HostingPanel contract, obsolete since SDK 3.1.0; the tester still takes the CWP client.
        var plugins = DetachedPanelPlugins.Build();
        var tester = new ServerConnectionTester(
            panel,
            provider.GetRequiredService<IHttpClientFactory>(),
            provider.GetRequiredService<ILoggerFactory>(),
            Options.Create(new ProvisioningOptions()),
            provider.GetRequiredService<ILogger<ServerConnectionTester>>(),
            plugins.Resolver,
            plugins.Caller);
#pragma warning restore CS0618
        return new TesterHost(provider, tester, logs);
    }

    /// <summary>A tester with the log it writes to, disposing the services it was built from.</summary>
    /// <param name="Provider">The services the tester's framework dependencies come from.</param>
    /// <param name="Tester">The tester under test.</param>
    /// <param name="Logs">What the tester logged.</param>
    private sealed record TesterHost(ServiceProvider Provider, ServerConnectionTester Tester, RecordingLoggerProvider Logs) : IAsyncDisposable
    {
        /// <summary>Disposes the services.</summary>
        /// <returns>A task that completes when the services are disposed.</returns>
        public ValueTask DisposeAsync() => Provider.DisposeAsync();
    }
}
