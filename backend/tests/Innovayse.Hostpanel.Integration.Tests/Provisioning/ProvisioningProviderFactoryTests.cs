using System.Security.Cryptography;
using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain;
using Innovayse.Hostpanel.Domain.Provisioning;
using Innovayse.Hostpanel.Domain.Servers;
using Innovayse.Hostpanel.Infrastructure.Integrations.CPanel;
using Innovayse.Hostpanel.Infrastructure.Integrations.CPanel.Whm;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.Hostpanel.Infrastructure.Provisioning;
using Innovayse.Hostpanel.Infrastructure.Provisioning.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Integration.Tests.Provisioning;

/// <summary>
/// Tests for <see cref="ProvisioningProviderFactory"/> with no panel plugin installed: which provider a server gets, and the
/// addresses the cPanel provider is built with, from the configured ports and never a literal.
/// </summary>
public sealed class ProvisioningProviderFactoryTests : IAsyncLifetime
{
    /// <summary>The loopback server standing in for the WHM host.</summary>
    private StubHttpServer whm = null!;

    /// <summary>Starts the stub WHM server before each test.</summary>
    /// <returns>A task that completes when the server listens.</returns>
    public async Task InitializeAsync()
    {
        whm = await StubHttpServer.StartAsync();
    }

    /// <summary>Stops the stub WHM server after each test.</summary>
    /// <returns>A task that completes when the server is down.</returns>
    public async Task DisposeAsync()
    {
        await whm.DisposeAsync();
    }

    /// <summary>Builds the server row a provider is created for.</summary>
    /// <param name="accessHash">The panel's access hash, or null for none.</param>
    /// <param name="module">The server's control panel module.</param>
    /// <returns>The server connection.</returns>
    private static ServerConnectionDto ServerRow(string? accessHash, string module = ServerModules.CPanel) =>
        new(1, "srv", module, "panel.example.test", null, accessHash);

    /// <summary>Builds the factory under test with the given options.</summary>
    /// <param name="http">The HTTP client factory the providers draw from.</param>
    /// <param name="options">The provisioning options.</param>
    /// <returns>The factory.</returns>
    private static ProvisioningProviderFactory BuildFactory(IHttpClientFactory http, ProvisioningOptions options)
    {
        // No plugin is installed, so every module is the host's own; the connection reader is never reached.
        var plugins = DetachedPanelPlugins.Build();
        return new ProvisioningProviderFactory(
            http,
            NullLoggerFactory.Instance,
            Options.Create(options),
            plugins.Resolver,
            plugins.Registry,
            plugins.Activator,
            plugins.Caller,
            null!,
            new PluginReferences(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))));
    }

    /// <summary>A module without a provider is answered as an unavailable panel, the code a plugin that is off answers.</summary>
    [Fact]
    public void CreateFor_Throws_ForAModuleWithoutAProvider()
    {
        var factory = BuildFactory(new StubHttpClientFactory(whm), new ProvisioningOptions());

        var act = () => factory.CreateFor(ServerRow("hash", TestPanelModules.Second));

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(HostpanelErrorCodes.PANEL_PLUGIN_UNAVAILABLE);
    }

    /// <summary>
    /// Every module the factory reports as supported is one it builds a provider for, so a product
    /// with no server group is never sent to a server the factory would then refuse.
    /// </summary>
    /// <param name="useFakeProvider">Whether the development fake provider is on.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SupportedModules_AreExactlyTheModulesTheFactoryBuildsAsync(bool useFakeProvider)
    {
        var factory = BuildFactory(new StubHttpClientFactory(whm), new ProvisioningOptions { UseFakeProvider = useFakeProvider });

        // A module the factory has no provider for is refused as an unavailable panel; any other failure (a builder
        // that dislikes the stand-in credentials) still means the factory knows the module.
        var built = ServerModules.Known.Where(module =>
        {
            try
            {
                factory.CreateFor(ServerRow("hash", module));
                return true;
            }
            catch (DomainRuleException ex) when (ex.Code == HostpanelErrorCodes.PANEL_PLUGIN_UNAVAILABLE)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return true;
            }
        });

        (await factory.SupportedModulesAsync(CancellationToken.None)).Should().BeEquivalentTo(built);
    }

    /// <summary>A cPanel server gets the WHM provider, calling WHM API 1 on the default WHM port with its token.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task CreateFor_BuildsTheCPanelProvider_OnTheDefaultWhmPortAsync()
    {
        var http = new StubHttpClientFactory(whm);
        whm.Enqueue("""{"metadata":{"result":1,"reason":"OK"}}""");
        var server = new ServerConnectionDto(1, "srv", ServerModules.CPanel, "panel.example.test", null, null, "root", "whm-token");
        var provider = BuildFactory(http, new ProvisioningOptions()).CreateFor(server);

        await provider.UnsuspendAsync(new UnsuspendAccountOrder(7, "testuser"), CancellationToken.None);

        provider.Should().BeOfType<WhmProvisioningProvider>();
        http.RequestedUris.Should().ContainSingle().Which.ToString()
            .Should().Be($"https://panel.example.test:{ProvisioningOptions.DefaultCPanelWhmPort}/json-api/unsuspendacct?api.version=1");
        whm.Requests.Should().ContainSingle().Which.Authorization.Should().Be("whm root:whm-token");
    }

    /// <summary>The configured WHM port is the one the cPanel provider calls.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task CreateFor_UsesTheConfiguredWhmPortAsync()
    {
        var http = new StubHttpClientFactory(whm);
        whm.Enqueue("""{"metadata":{"result":1,"reason":"OK"}}""");
        var server = new ServerConnectionDto(1, "srv", ServerModules.CPanel, "panel.example.test", null, null, "root", "whm-token");
        var provider = BuildFactory(http, new ProvisioningOptions { CPanelWhmPort = 2999 }).CreateFor(server);

        await provider.UnsuspendAsync(new UnsuspendAccountOrder(7, "testuser"), CancellationToken.None);

        http.RequestedUris.Should().ContainSingle().Which.Port.Should().Be(2999);
    }

    /// <summary>A cPanel server with an API address has WHM dialled at that address, not at its host name.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task CreateFor_DialsTheApiHost_WhenTheServerHasOneAsync()
    {
        var http = new StubHttpClientFactory(whm);
        whm.Enqueue("""{"metadata":{"result":1,"reason":"OK"}}""");
        var server = new ServerConnectionDto(1, "srv", ServerModules.CPanel, "panel.example.test", null, null, "root", "whm-token", ApiHost: "10.77.0.1");
        var provider = BuildFactory(http, new ProvisioningOptions()).CreateFor(server);

        await provider.UnsuspendAsync(new UnsuspendAccountOrder(7, "testuser"), CancellationToken.None);

        http.RequestedUris.Should().ContainSingle().Which.Host.Should().Be("10.77.0.1");
    }

    /// <summary>A server saved in a different case still gets the built in cPanel provider: modules compare ignoring case.</summary>
    [Fact]
    public void CreateFor_MatchesTheBuiltInModuleIgnoringCase()
    {
        var server = new ServerConnectionDto(1, "srv", "CPANEL", "panel.example.test", null, null, "root", "whm-token");

        var provider = BuildFactory(new StubHttpClientFactory(whm), new ProvisioningOptions()).CreateFor(server);

        provider.Should().BeOfType<WhmProvisioningProvider>();
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
        var factory = BuildFactory(new StubHttpClientFactory(whm), new ProvisioningOptions());
        var server = new ServerConnectionDto(1, "srv", ServerModules.CPanel, "panel.example.test", null, "hash", username, apiToken);

        var act = () => factory.CreateFor(server);

        act.Should().Throw<InvalidOperationException>().WithMessage("*no WHM username or API token*");
    }

    /// <summary>The server row prints without its API token, so a log line of it leaks nothing.</summary>
    [Fact]
    public void ServerConnection_ToString_HidesTheApiToken()
    {
        var server = new ServerConnectionDto(1, "srv", ServerModules.CPanel, "panel.example.test", null, null, "root", "whm-secret-token");

        server.ToString().Should().NotContain("whm-secret-token").And.Contain("root");
    }

    /// <summary>The development switch returns the in-process provider and never builds a panel client.</summary>
    [Fact]
    public void CreateFor_ReturnsTheNullProvider_WhenTheFakeProviderIsOn()
    {
        var factory = BuildFactory(new StubHttpClientFactory(whm), new ProvisioningOptions { UseFakeProvider = true });

        var provider = factory.CreateFor(ServerRow(null));

        provider.Should().BeOfType<NullCPanelProvisioningProvider>();
    }
}
