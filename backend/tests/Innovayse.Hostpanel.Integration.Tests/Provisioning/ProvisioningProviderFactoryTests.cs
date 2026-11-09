using Hostpanel.Sdk.Plugins.HostingPanel;
using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Domain.Servers.Enums;
using Innovayse.Hostpanel.Infrastructure.Integrations.CPanel;
using Innovayse.Hostpanel.Infrastructure.Integrations.CPanel.Whm;
using Innovayse.Hostpanel.Infrastructure.Integrations.Plesk;
using Innovayse.Hostpanel.Infrastructure.Provisioning;
using Innovayse.Hostpanel.Infrastructure.Provisioning.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Integration.Tests.Provisioning;

/// <summary>
/// Tests for <see cref="ProvisioningProviderFactory"/>: which provider a server gets, and the
/// addresses the version 7 hosting panel provider is built with, from the configured ports and never a literal.
/// </summary>
public sealed class ProvisioningProviderFactoryTests : IAsyncLifetime
{
    /// <summary>The loopback server standing in for the hosting panel host.</summary>
    private StubHttpServer cwp7 = null!;

    /// <summary>Starts the stub hosting panel server before each test.</summary>
    /// <returns>A task that completes when the server listens.</returns>
    public async Task InitializeAsync()
    {
        cwp7 = await StubHttpServer.StartAsync();
    }

    /// <summary>Stops the stub hosting panel server after each test.</summary>
    /// <returns>A task that completes when the server is down.</returns>
    public async Task DisposeAsync()
    {
        await cwp7.DisposeAsync();
    }

    /// <summary>Builds the server row a version 7 hosting panel provider is created for.</summary>
    /// <param name="accessHash">The panel's access hash, or null for none.</param>
    /// <param name="module">The server's control panel module.</param>
    /// <returns>The server connection.</returns>
    private static ServerConnectionDto ServerRow(string? accessHash, ServerModule module = ServerModule.Cwp7) =>
        new(1, "srv", module, "panel.example.test", null, accessHash);

    /// <summary>Builds the factory under test with the given options.</summary>
    /// <param name="http">The HTTP client factory the providers draw from.</param>
    /// <param name="options">The provisioning options.</param>
    /// <returns>The factory.</returns>
    private static ProvisioningProviderFactory BuildFactory(IHttpClientFactory http, ProvisioningOptions options) =>
        new(http, NullLoggerFactory.Instance, Options.Create(options));

#pragma warning disable CS0618 // Legacy HostingPanel contract, obsolete since SDK 3.1.0; stays until the CWP providers become IProvisioningPanelPlugin plugins.
    /// <summary>With no port configured, the provider calls the contract's default API port.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task CreateFor_UsesTheDefaultApiPort_WhenNoPortIsConfiguredAsync()
    {
        var http = new StubHttpClientFactory(cwp7);
        cwp7.Enqueue("""{ "status": "OK", "msj": "Done." }""");
        var provider = BuildFactory(http, new ProvisioningOptions()).CreateFor(ServerRow("hash"));

        await provider.UnsuspendAsync("testuser", CancellationToken.None);

        http.RequestedUris.Should().ContainSingle().Which.ToString()
            .Should().Be($"https://panel.example.test:{IHostingPanelApiClient.DefaultApiPort}/v1/account");
    }
#pragma warning restore CS0618

    /// <summary>A configured API port is the one the provider calls.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task CreateFor_UsesTheConfiguredApiPortAsync()
    {
        var http = new StubHttpClientFactory(cwp7);
        cwp7.Enqueue("""{ "status": "OK", "msj": "Done." }""");
        var provider = BuildFactory(http, new ProvisioningOptions { Cwp7ApiPort = 2400 }).CreateFor(ServerRow("hash"));

        await provider.UnsuspendAsync("testuser", CancellationToken.None);

        http.RequestedUris.Should().ContainSingle().Which.Port.Should().Be(2400);
    }

    /// <summary>The sign-in address is built from the user-panel port option.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task CreateFor_BuildsTheSignInAddressFromTheUserPanelPortAsync()
    {
        var provider = BuildFactory(new StubHttpClientFactory(cwp7), new ProvisioningOptions { Cwp7UserPanelPort = 2096 })
            .CreateFor(ServerRow("hash"));

        var credentials = await provider.GetCredentialsAsync("testuser", CancellationToken.None);

        credentials.CpanelUrl.Should().Be("https://panel.example.test:2096");
        credentials.Password.Should().BeNull();
    }

    /// <summary>With no port configured the sign-in address uses the user-panel default.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task CreateFor_UsesTheDefaultUserPanelPort_WhenNoPortIsConfiguredAsync()
    {
        var provider = BuildFactory(new StubHttpClientFactory(cwp7), new ProvisioningOptions()).CreateFor(ServerRow("hash"));

        var credentials = await provider.GetCredentialsAsync("testuser", CancellationToken.None);

        credentials.CpanelUrl.Should().Be($"https://panel.example.test:{ProvisioningOptions.DefaultCwp7UserPanelPort}");
    }

    /// <summary>A server with a blank access hash is refused when the provider is built.</summary>
    [Fact]
    public void CreateFor_Throws_WhenTheServerHasNoAccessHash()
    {
        var factory = BuildFactory(new StubHttpClientFactory(cwp7), new ProvisioningOptions());

        var act = () => factory.CreateFor(ServerRow("  "));

        act.Should().Throw<InvalidOperationException>().WithMessage("*no access hash*");
    }

    /// <summary>A module without a provider is refused by name.</summary>
    [Fact]
    public void CreateFor_Throws_ForAModuleWithoutAProvider()
    {
        var factory = BuildFactory(new StubHttpClientFactory(cwp7), new ProvisioningOptions());

        var act = () => factory.CreateFor(ServerRow("hash", ServerModule.DirectAdmin));

        act.Should().Throw<NotSupportedException>();
    }

    /// <summary>
    /// Every module the factory reports as supported is one it builds a provider for, so a product
    /// with no server group is never sent to a server the factory would then refuse.
    /// </summary>
    /// <param name="useFakeProvider">Whether the development fake provider is on.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SupportedModules_AreExactlyTheModulesTheFactoryBuilds(bool useFakeProvider)
    {
        var factory = BuildFactory(new StubHttpClientFactory(cwp7), new ProvisioningOptions { UseFakeProvider = useFakeProvider });

        // A module the factory refuses throws NotSupportedException; any other failure (a builder
        // that dislikes the stand-in credentials) still means the factory knows the module.
        var built = Enum.GetValues<ServerModule>().Where(module =>
        {
            try
            {
                factory.CreateFor(ServerRow("hash", module));
                return true;
            }
            catch (NotSupportedException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return true;
            }
        });

        factory.SupportedModules.Should().BeEquivalentTo(built);
    }

    /// <summary>A Plesk server gets the Plesk provider, calling the XML API on the default Plesk port.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task CreateFor_BuildsThePleskProvider_OnTheDefaultPleskPortAsync()
    {
        var http = new StubHttpClientFactory(cwp7);
        cwp7.Enqueue(System.Net.HttpStatusCode.OK, "<packet><customer><set><result><status>ok</status></result></set></customer></packet>", "text/xml");
        var provider = BuildFactory(http, new ProvisioningOptions()).CreateFor(ServerRow("plesk-key", ServerModule.Plesk));

        await provider.UnsuspendAsync("testuser", CancellationToken.None);

        provider.Should().BeOfType<PleskProvisioningProvider>();
        http.RequestedUris.Should().ContainSingle().Which.ToString()
            .Should().Be($"https://panel.example.test:{ProvisioningOptions.DefaultPleskPort}/enterprise/control/agent.php");
    }

    /// <summary>A Plesk server with a blank API key is refused when the provider is built.</summary>
    [Fact]
    public void CreateFor_Throws_WhenAPleskServerHasNoApiKey()
    {
        var factory = BuildFactory(new StubHttpClientFactory(cwp7), new ProvisioningOptions());

        var act = () => factory.CreateFor(ServerRow(" ", ServerModule.Plesk));

        act.Should().Throw<InvalidOperationException>().WithMessage("*no access hash*");
    }

    /// <summary>A cPanel server gets the WHM provider, calling WHM API 1 on the default WHM port with its token.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task CreateFor_BuildsTheCPanelProvider_OnTheDefaultWhmPortAsync()
    {
        var http = new StubHttpClientFactory(cwp7);
        cwp7.Enqueue("""{"metadata":{"result":1,"reason":"OK"}}""");
        var server = new ServerConnectionDto(1, "srv", ServerModule.CPanel, "panel.example.test", null, null, "root", "whm-token");
        var provider = BuildFactory(http, new ProvisioningOptions()).CreateFor(server);

        await provider.UnsuspendAsync("testuser", CancellationToken.None);

        provider.Should().BeOfType<WhmProvisioningProvider>();
        http.RequestedUris.Should().ContainSingle().Which.ToString()
            .Should().Be($"https://panel.example.test:{ProvisioningOptions.DefaultCPanelWhmPort}/json-api/unsuspendacct?api.version=1");
        cwp7.Requests.Should().ContainSingle().Which.Authorization.Should().Be("whm root:whm-token");
    }

    /// <summary>A cPanel server without a WHM username or API token is refused when the provider is built.</summary>
    /// <param name="username">The server's WHM username.</param>
    /// <param name="apiToken">The server's API token.</param>
    [Theory]
    [InlineData("root", null)]
    [InlineData("root", " ")]
    [InlineData(null, "whm-token")]
    public void CreateFor_Throws_WhenACPanelServerHasNoUsernameOrToken(string? username, string? apiToken)
    {
        var factory = BuildFactory(new StubHttpClientFactory(cwp7), new ProvisioningOptions());
        var server = new ServerConnectionDto(1, "srv", ServerModule.CPanel, "panel.example.test", null, "hash", username, apiToken);

        var act = () => factory.CreateFor(server);

        act.Should().Throw<InvalidOperationException>().WithMessage("*no WHM username or API token*");
    }

    /// <summary>The server row prints without its API token, so a log line of it leaks nothing.</summary>
    [Fact]
    public void ServerConnection_ToString_HidesTheApiToken()
    {
        var server = new ServerConnectionDto(1, "srv", ServerModule.CPanel, "panel.example.test", null, null, "root", "whm-secret-token");

        server.ToString().Should().NotContain("whm-secret-token").And.Contain("root");
    }

    /// <summary>The development switch returns the in-process provider and never builds a panel client.</summary>
    [Fact]
    public void CreateFor_ReturnsTheNullProvider_WhenTheFakeProviderIsOn()
    {
        var factory = BuildFactory(new StubHttpClientFactory(cwp7), new ProvisioningOptions { UseFakeProvider = true });

        var provider = factory.CreateFor(ServerRow(null));

        provider.Should().BeOfType<NullCPanelProvisioningProvider>();
    }
}
