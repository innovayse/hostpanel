using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Manifest;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Innovayse.Hostpanel.Integration.Tests.Plugins;

/// <summary>
/// The admin route door with the <c>endpoints</c> capability (#818), against Postgres and a real host: a fulfilment
/// plugin declaring the capability is served, and the same plugin without it, or declaring it on a class that does not
/// implement <c>IPluginEndpoints</c>, answers the same 404 body as an id that was never loaded.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class FulfilmentEndpointsPluginRouteTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// Builds a host whose registry holds three fulfilment plugins, all switched on: one serving, one without the
    /// capability, and one declaring it on a class that implements only <c>IFulfilmentPlugin</c>.
    /// </summary>
    /// <param name="serving">The id of the plugin that declares <c>endpoints</c> and implements it.</param>
    /// <param name="plain">The id of the plugin that does not declare it.</param>
    /// <param name="broken">The id of the plugin that declares it without implementing it.</param>
    /// <returns>The host, to dispose when the test ends.</returns>
    private async Task<WebApplicationFactory<Program>> HostAsync(string serving, string plain, string broken)
    {
        var registry = PluginActivationTestSeeds.Registry(
            PluginCapabilitySeeds.Plugin(serving, PluginType.Fulfilment, typeof(FulfilmentEndpointsPlugin), PluginCapabilities.Endpoints),
            PluginCapabilitySeeds.Plugin(plain, PluginType.Fulfilment, typeof(FulfilmentEndpointsPlugin)),
            PluginCapabilitySeeds.Plugin(
                broken, PluginType.Fulfilment, typeof(ConfigurationCapturingFulfilmentPlugin), PluginCapabilities.Endpoints));
        using (var scope = factory.Services.CreateScope())
        {
            await PluginActivationTestSeeds.EnableAsync(
                scope.ServiceProvider.GetRequiredService<AppDbContext>(), serving, plain, broken);
        }

        return factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<PluginRegistry>();
            services.AddSingleton(registry);
        }));
    }

    /// <summary>Calls the declared admin route of a plugin as an administrator.</summary>
    /// <param name="host">The host.</param>
    /// <param name="pluginId">The plugin id.</param>
    /// <returns>The response.</returns>
    private async Task<HttpResponseMessage> CallAsync(WebApplicationFactory<Program> host, string pluginId)
    {
        var http = host.CreateClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await factory.GetAdminTokenAsync());
        return await http.GetAsync($"/api/admin/plugins/{pluginId}/ext{PluginCapabilitySeeds.AdminPath}");
    }

    /// <summary>A fulfilment plugin with the capability answers its declared route.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task FulfilmentWithEndpoints_IsServed()
    {
        var serving = PluginActivationTestSeeds.NewId("sell");
        using var host = await HostAsync(
            serving, PluginActivationTestSeeds.NewId("plain"), PluginActivationTestSeeds.NewId("broken"));

        using var response = await CallAsync(host, serving);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>
    /// Without the capability, or with it declared on a class that does not implement it, the route answers the very
    /// 404 body an unknown plugin id answers.
    /// </summary>
    /// <param name="broken">Whether the refused plugin declares the capability without implementing it.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FulfilmentThatDoesNotServe_AnswersTheUnknownPlugin404(bool broken)
    {
        var plain = PluginActivationTestSeeds.NewId("plain");
        var brokenId = PluginActivationTestSeeds.NewId("broken");
        using var host = await HostAsync(PluginActivationTestSeeds.NewId("sell"), plain, brokenId);

        using var refused = await CallAsync(host, broken ? brokenId : plain);
        using var unknown = await CallAsync(host, "never-loaded");

        refused.StatusCode.Should().Be(HttpStatusCode.NotFound);
        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var refusedBody = JsonDocument.Parse(await refused.Content.ReadAsStringAsync());
        using var unknownBody = JsonDocument.Parse(await unknown.Content.ReadAsStringAsync());
        refusedBody.RootElement.GetProperty("type").GetString().Should().Be(HostpanelErrorCodes.PLUGIN_ROUTE_NOT_FOUND);
        // The four ErrorResponse members only: a per-request member added later (a trace id) must not fail this test.
        foreach (var member in new[] { "type", "title", "status", "detail" })
        {
            refusedBody.RootElement.GetProperty(member).GetRawText().Should().Be(unknownBody.RootElement.GetProperty(member).GetRawText());
        }
    }
}
