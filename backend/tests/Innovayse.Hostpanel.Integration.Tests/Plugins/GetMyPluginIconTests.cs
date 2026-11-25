using System.Net;
using System.Text;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Plugins;

/// <summary>
/// <c>GET /api/me/plugins/{id}/icon</c>: a signed-in client reads the icon an installed plugin ships, so the portal's Open panel
/// button can show it; an unknown plugin and a plugin without an icon answer the same 404; the route is closed to anonymous callers.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetMyPluginIconTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A small SVG icon.</summary>
    private const string Svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 8 8\"><path d=\"M0 0h8v8z\"/></svg>";

    /// <summary>Builds a client-role client over a host whose loaded plugins come from the given fake.</summary>
    /// <param name="registry">The fake registry.</param>
    /// <returns>The signed-in client.</returns>
    private async Task<HttpClient> ClientOverAsync(FakePluginRegistry registry)
    {
        var owner = await ServicesTestSeeds.RegisterOwnerAsync(factory, "plugin-icon-client");
        var client = factory
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddSingleton<IPluginRegistry>(registry)))
            .CreateClient();
        client.DefaultRequestHeaders.Authorization = owner.Client.DefaultRequestHeaders.Authorization;
        return client;
    }

    /// <summary>A client reads a plugin's icon with the safe headers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Client_ReadsTheIconWithSafeHeadersAsync()
    {
        var registry = new FakePluginRegistry();
        registry.Add(PluginManifestReader.Deserialize(PluginsTestSeeds.ManifestJson("svg-plugin"))!);
        registry.AddIcon("svg-plugin", new PluginIconDto("image/svg+xml", Encoding.UTF8.GetBytes(Svg), "\"abc\""));
        var client = await ClientOverAsync(registry);

        var response = await client.GetAsync("/api/me/plugins/svg-plugin/icon");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/svg+xml");
        response.Headers.GetValues("X-Content-Type-Options").Should().ContainSingle().Which.Should().Be("nosniff");
        response.Headers.ETag!.Tag.Should().Be("\"abc\"");
        (await response.Content.ReadAsStringAsync()).Should().Be(Svg);
    }

    /// <summary>A plugin without an icon and an unknown plugin answer the same 404 and the same code.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task NoIconAndUnknownPlugin_AnswerTheSame404Async()
    {
        var registry = new FakePluginRegistry();
        registry.Add(PluginManifestReader.Deserialize(PluginsTestSeeds.ManifestJson("plain-plugin"))!);
        var client = await ClientOverAsync(registry);

        var withoutIcon = await client.GetAsync("/api/me/plugins/plain-plugin/icon");
        var unknown = await client.GetAsync("/api/me/plugins/not-installed/icon");

        withoutIcon.StatusCode.Should().Be(HttpStatusCode.NotFound);
        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await IntegrationsTestSeeds.AssertCodeAsync(withoutIcon, HostpanelErrorCodes.PLUGIN_ICON_NOT_FOUND);
        await IntegrationsTestSeeds.AssertCodeAsync(unknown, HostpanelErrorCodes.PLUGIN_ICON_NOT_FOUND);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().GetAsync("/api/me/plugins/any-plugin/icon");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
