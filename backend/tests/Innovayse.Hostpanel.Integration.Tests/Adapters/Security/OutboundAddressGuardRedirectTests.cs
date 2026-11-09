using System.Net;
using Innovayse.Hostpanel.Infrastructure.Integrations.CPanel.Whm.Models;
using Innovayse.Hostpanel.Infrastructure.Security;
using Innovayse.Hostpanel.Infrastructure.Security.Extensions;
using Innovayse.Hostpanel.Infrastructure.Security.Settings;
using Innovayse.Hostpanel.Integration.Tests.Adapters.Integrations.CPanel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Security;

/// <summary>
/// Tests whether a client registered with <see cref="OutboundHttpClientBuilderExtensions.AddOutboundAddressGuard"/>
/// follows a redirect: the WHM client must not, because its request carries a password and a token.
/// The WHM test builds the client through <c>AddCPanelWhmHttpClient</c>, the registration the
/// application uses, so dropping <c>followRedirects: false</c> there turns it red.
/// </summary>
public sealed class OutboundAddressGuardRedirectTests
{
    /// <summary>The name the stub servers are reached by; the panel route sends it to loopback.</summary>
    private const string PanelHost = "whm.invalid";

    /// <summary>A WHM reply that reports success, served by the second location.</summary>
    private const string Ok = """{"metadata":{"result":1}}""";

    /// <summary>A 307 from the WHM server is an unreadable reply and the second location receives nothing.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Call_TemporaryRedirect_IsUnreadableAndTheOtherLocationGetsNothingAsync()
    {
        await using var whm = await StubHttpServer.StartAsync();
        await using var other = await StubHttpServer.StartAsync();
        whm.Enqueue(RedirectTo(other));
        other.Enqueue(Ok);
        await using var host = WhmTestHost.Build();

        var reply = await host.Client().CreateAccountAsync(
            WhmTestHost.ConnectionTo(whm), "testuser", "example.com", "S3cret-pass", "Basic", CancellationToken.None);

        reply.Should().Be(WhmReply.Unreadable);
        whm.Requests.Should().ContainSingle();
        other.Requests.Should().BeEmpty();
    }

    /// <summary>With redirects left on, the same 307 does reach the second location, so the test above proves the option.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Call_TemporaryRedirect_WhenFollowedReachesTheOtherLocationAsync()
    {
        await using var whm = await StubHttpServer.StartAsync();
        await using var other = await StubHttpServer.StartAsync();
        whm.Enqueue(RedirectTo(other));
        other.Enqueue(Ok);
        await using var provider = BuildProvider(followRedirects: true);
        using var http = provider.GetRequiredService<IHttpClientFactory>().CreateClient("whm");

        using var response = await http.PostAsync(
            $"http://{PanelHost}:{whm.BaseAddress.Port}/json-api/passwd?api.version=1", new StringContent("password=x"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        other.Requests.Should().ContainSingle();
    }

    /// <summary>Builds a 307 reply that points at another stub server.</summary>
    /// <param name="target">The server the redirect points at.</param>
    /// <returns>The scripted reply.</returns>
    private static StubReply RedirectTo(StubHttpServer target) =>
        new(
            HttpStatusCode.TemporaryRedirect,
            string.Empty,
            Headers: new Dictionary<string, string> { ["Location"] = $"http://{PanelHost}:{target.BaseAddress.Port}/json-api/createacct?api.version=1" });

    /// <summary>
    /// Registers a named client the way the application registers the WHM one, with the panel host
    /// routed to loopback so the connector lets the stub servers be dialled.
    /// </summary>
    /// <param name="followRedirects">Whether the guarded client follows redirects.</param>
    /// <returns>The built provider.</returns>
    private static ServiceProvider BuildProvider(bool followRedirects)
    {
        var services = new ServiceCollection();
        services.AddSingleton(Options.Create(new OutboundNetworkOptions()));
        services.AddSingleton(Options.Create(new PanelNetworkOptions { HostAddresses = $"{PanelHost}=127.0.0.1" }));
        services.AddSingleton<OutboundConnector>();
        services.AddHttpClient("whm").AddOutboundAddressGuard(followRedirects);
        return services.BuildServiceProvider();
    }
}
