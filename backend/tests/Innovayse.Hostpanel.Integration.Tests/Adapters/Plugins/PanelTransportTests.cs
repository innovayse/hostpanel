using System.Net;
using System.Security.Authentication;
using Hostpanel.Sdk.Plugins.Provisioning;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Servers;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.Hostpanel.Infrastructure.Plugins.Extensions;
using Innovayse.Hostpanel.Infrastructure.Resilience;
using Innovayse.Hostpanel.Infrastructure.Security;
using Innovayse.Hostpanel.Infrastructure.Security.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// <see cref="PanelTransport"/>, the client every panel, virtual server and registrar plugin reaches its servers through:
/// loopback and metadata addresses are refused, a redirect is handed back and not followed, a failed request is not repeated,
/// both kinds of client carry the backstop timeout, and the operator's "accept a self signed certificate" setting makes one
/// server's host name trusted and nothing else. The registration under test is <see cref="PanelTransportExtensions.AddPanelTransport"/>,
/// the one the application uses; the servers are real Kestrel listeners reached through the panel route map.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host, whose database holds the servers.</param>
public sealed class PanelTransportTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The name the stub servers are reached by; the panel route sends it to loopback.</summary>
    private const string PanelHost = "panel.invalid";

    /// <summary>A second name for the same listener, which the operator never trusted.</summary>
    private const string OtherHost = "other.invalid";

    /// <summary>A loopback literal and a metadata address are refused before any connection, and say why.</summary>
    /// <param name="address">The address a plugin asks for.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData("http://127.0.0.1:9/")]
    [InlineData("http://[::1]:9/")]
    [InlineData("http://169.254.169.254/latest/meta-data/")]
    [InlineData("http://100.100.100.200/")]
    [InlineData("http://[fd00:ec2::254]/")]
    public async Task Client_LoopbackAndMetadataAddresses_AreRefusedAsync(string address)
    {
        using var scope = factory.Services.CreateScope();
        await using var provider = BuildProvider();
        using var http = provider.GetRequiredService<IPanelTransport>().CreateClient();

        var request = () => http.GetAsync(address);

        (await request.Should().ThrowAsync<HttpRequestException>()).Which.InnerException.Should().BeOfType<OutboundAddressRefusedException>();
    }

    /// <summary>The same refusal holds for the client of a server whose certificate the operator trusts.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task TrustingClient_LoopbackAddress_IsRefusedAsync()
    {
        using var scope = factory.Services.CreateScope();
        var serverId = await SeedServerAsync(scope, PanelHost, acceptsSelfSigned: true);
        await using var provider = BuildProvider();
        using var http = provider.GetRequiredService<IPanelTransport>().CreateClient(Connection(serverId, PanelHost));

        var request = () => http.GetAsync("https://127.0.0.1:9/");

        (await request.Should().ThrowAsync<HttpRequestException>()).Which.InnerException.Should().BeOfType<OutboundAddressRefusedException>();
    }

    /// <summary>A 302 reaches the plugin as it is and the address it names receives nothing.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Client_Redirect_IsHandedBackAndNotFollowedAsync()
    {
        await using var panel = await StubHttpServer.StartAsync();
        await using var other = await StubHttpServer.StartAsync();
        panel.Enqueue(new StubReply(
            HttpStatusCode.Found,
            string.Empty,
            Headers: new Dictionary<string, string> { ["Location"] = $"http://{PanelHost}:{other.BaseAddress.Port}/elsewhere" }));
        other.Enqueue("{}");
        using var scope = factory.Services.CreateScope();
        await using var provider = BuildProvider();
        using var http = provider.GetRequiredService<IPanelTransport>().CreateClient();

        using var response = await http.GetAsync($"http://{PanelHost}:{panel.BaseAddress.Port}/start");

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        panel.Requests.Should().ContainSingle();
        other.Requests.Should().BeEmpty();
    }

    /// <summary>A server error is answered once: the transport never repeats a request, so a panel call is never made twice.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Client_ServerError_IsNotRepeatedAsync()
    {
        await using var panel = await StubHttpServer.StartAsync();
        panel.Enqueue(HttpStatusCode.ServiceUnavailable, "{}");
        using var scope = factory.Services.CreateScope();
        await using var provider = BuildProvider();
        using var http = provider.GetRequiredService<IPanelTransport>().CreateClient();

        using var response = await http.PostAsync($"http://{PanelHost}:{panel.BaseAddress.Port}/createacct", new StringContent("{}"));

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        panel.Requests.Should().ContainSingle();
    }

    /// <summary>Both kinds of client carry the backstop timeout, so a stalled panel cannot hold a request thread for the default 100 seconds.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Client_BothKinds_CarryTheBackstopTimeoutAsync()
    {
        using var scope = factory.Services.CreateScope();
        var serverId = await SeedServerAsync(scope, PanelHost, acceptsSelfSigned: true);
        await using var provider = BuildProvider();
        var transport = provider.GetRequiredService<IPanelTransport>();

        using var plain = transport.CreateClient();
        using var trusting = transport.CreateClient(Connection(serverId, PanelHost));

        plain.Timeout.Should().Be(HttpBackstopTimeouts.PanelTransport);
        trusting.Timeout.Should().Be(HttpBackstopTimeouts.PanelTransport);
    }

    /// <summary>A self signed certificate is refused by the plain client, whatever the operator set for a server.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task PlainClient_SelfSignedCertificate_IsRefusedAsync()
    {
        await using var panel = await SelfSignedHttpsServer.StartAsync(PanelHost);
        using var scope = factory.Services.CreateScope();
        await SeedServerAsync(scope, PanelHost, acceptsSelfSigned: true);
        await using var provider = BuildProvider();
        using var http = provider.GetRequiredService<IPanelTransport>().CreateClient();

        var request = () => http.GetAsync($"https://{PanelHost}:{panel.Port}/");

        (await request.Should().ThrowAsync<HttpRequestException>()).Which.InnerException.Should().BeOfType<AuthenticationException>();
        panel.Paths.Should().BeEmpty();
    }

    /// <summary>The client of a server the operator did not allow refuses the certificate, and so does one for a server the host does not know.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ServerClient_SettingOffOrServerUnknown_RefusesTheCertificateAsync()
    {
        await using var panel = await SelfSignedHttpsServer.StartAsync(PanelHost);
        using var scope = factory.Services.CreateScope();
        var strictId = await SeedServerAsync(scope, PanelHost, acceptsSelfSigned: false);
        await using var provider = BuildProvider();
        var transport = provider.GetRequiredService<IPanelTransport>();
        using var strict = transport.CreateClient(Connection(strictId, PanelHost));
        using var unknown = transport.CreateClient(Connection(int.MaxValue, PanelHost));

        foreach (var http in new[] { strict, unknown })
        {
            var request = () => http.GetAsync($"https://{PanelHost}:{panel.Port}/");
            (await request.Should().ThrowAsync<HttpRequestException>()).Which.InnerException.Should().BeOfType<AuthenticationException>();
        }

        panel.Paths.Should().BeEmpty();
    }

    /// <summary>The client of a server the operator allowed completes the handshake with its self signed certificate.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ServerClient_SettingOn_AcceptsTheCertificateOfThatHostAsync()
    {
        await using var panel = await SelfSignedHttpsServer.StartAsync(PanelHost);
        using var scope = factory.Services.CreateScope();
        var serverId = await SeedServerAsync(scope, PanelHost, acceptsSelfSigned: true);
        await using var provider = BuildProvider();
        using var http = provider.GetRequiredService<IPanelTransport>().CreateClient(Connection(serverId, PanelHost));

        using var response = await http.GetAsync($"https://{PanelHost}:{panel.Port}/ping");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        panel.Paths.Should().Equal("/ping");
    }

    /// <summary>
    /// The allowance is the stored host name of that one server: the same client refuses the same certificate under another
    /// name, and a connection that names another host (a plugin passing a forged one) does not move the allowance.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ServerClient_SettingOn_RefusesTheCertificateUnderAnotherNameAsync()
    {
        await using var panel = await SelfSignedHttpsServer.StartAsync(PanelHost);
        using var scope = factory.Services.CreateScope();
        var serverId = await SeedServerAsync(scope, PanelHost, acceptsSelfSigned: true);
        await using var provider = BuildProvider();
        using var forged = provider.GetRequiredService<IPanelTransport>().CreateClient(Connection(serverId, OtherHost));

        var elsewhere = () => forged.GetAsync($"https://{OtherHost}:{panel.Port}/");

        (await elsewhere.Should().ThrowAsync<HttpRequestException>()).Which.InnerException.Should().BeOfType<AuthenticationException>();
        panel.Paths.Should().BeEmpty();
        using var stored = await forged.GetAsync($"https://{PanelHost}:{panel.Port}/");
        stored.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>Trusting one server's certificate does not trust it for the next client built for a server that is not allowed.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ServerClient_TrustDoesNotLeakToTheNextClientAsync()
    {
        await using var panel = await SelfSignedHttpsServer.StartAsync(PanelHost);
        using var scope = factory.Services.CreateScope();
        var trustedId = await SeedServerAsync(scope, PanelHost, acceptsSelfSigned: true);
        var strictId = await SeedServerAsync(scope, PanelHost, acceptsSelfSigned: false);
        await using var provider = BuildProvider();
        var transport = provider.GetRequiredService<IPanelTransport>();
        using var trusting = transport.CreateClient(Connection(trustedId, PanelHost));
        using var ok = await trusting.GetAsync($"https://{PanelHost}:{panel.Port}/");
        using var strict = transport.CreateClient(Connection(strictId, PanelHost));

        var request = () => strict.GetAsync($"https://{PanelHost}:{panel.Port}/");

        ok.StatusCode.Should().Be(HttpStatusCode.OK);
        (await request.Should().ThrowAsync<HttpRequestException>()).Which.InnerException.Should().BeOfType<AuthenticationException>();
    }

    /// <summary>Builds the plugin's view of a server, as the host would hand it over.</summary>
    /// <param name="serverId">The id of the stored server.</param>
    /// <param name="hostname">The host name the connection carries.</param>
    /// <returns>The connection.</returns>
    private static PanelServerConnection Connection(int serverId, string hostname)
    {
        return new PanelServerConnection(serverId, "panel", hostname, null, null, true, "root", null, null);
    }

    /// <summary>Stores a server with the given host name and the operator's setting.</summary>
    /// <param name="scope">The scope whose data context saves the server.</param>
    /// <param name="hostname">The host name of the server.</param>
    /// <param name="acceptsSelfSigned">Whether the operator trusts the server's self signed certificate.</param>
    /// <returns>The id of the new server.</returns>
    private static async Task<int> SeedServerAsync(IServiceScope scope, string hostname, bool acceptsSelfSigned)
    {
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var server = Server.Create(
            new ServerDetails(
                $"transport-{Guid.NewGuid():N}", hostname, null, null, ServerModules.Cwp7, "root", ServerCredentials.None, true, null,
                false, false, 0m, null, null, ServerNameServers.None, acceptsSelfSigned),
            TestMoments.Seeded);
        context.Servers.Add(server);
        await context.SaveChangesAsync(CancellationToken.None);
        return server.Id;
    }

    /// <summary>
    /// Registers the transport the way the application does, over the host's database, with both panel names routed to
    /// loopback so the connector lets the listeners be dialled.
    /// </summary>
    /// <returns>The built provider.</returns>
    private ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Options.Create(new OutboundNetworkOptions()));
        services.AddSingleton(Options.Create(new PanelNetworkOptions { HostAddresses = $"{PanelHost}=127.0.0.1,{OtherHost}=127.0.0.1" }));
        services.AddSingleton<OutboundConnector>();
        // The transport reads on a context of its own per call, so each one is a fresh context of the host's database.
        services.AddScoped(_ => factory.Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>());
        services.AddPanelTransport();

        // The transport is scoped like the plugins that take it; a test resolves it from the root, which is the one scope it uses.
        return services.BuildServiceProvider(validateScopes: false);
    }
}
