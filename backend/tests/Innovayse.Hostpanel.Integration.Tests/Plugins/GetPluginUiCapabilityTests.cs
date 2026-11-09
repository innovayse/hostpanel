using System.Net;
using System.Text.Json;
using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Manifest;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Innovayse.Hostpanel.Integration.Tests.Plugins;

/// <summary>
/// The plugin UI index and page (#643) with the <c>endpoints</c> capability (#818), against Postgres: a fulfilment plugin
/// declaring it lists and serves its page; the same plugin without it, or declaring it on a class that does not implement
/// <c>IPluginEndpoints</c>, is absent and its page answers the same 404 body as an unknown id.
/// </summary>
public sealed class GetPluginUiCapabilityTests : IClassFixture<PluginUiFixture>
{
    /// <summary>The page every plugin of this test declares.</summary>
    private const string PageId = "credits";

    /// <summary>The client index address.</summary>
    private const string ClientIndexUrl = "/api/client/plugins/ui";

    /// <summary>The shared factory and sign-in helpers; its own registry is not used here.</summary>
    private readonly PluginUiFixture _fixture;

    /// <summary>Initializes the test.</summary>
    /// <param name="fixture">The shared factory and sign-in helpers.</param>
    public GetPluginUiCapabilityTests(PluginUiFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>A fulfilment plugin with one client page, with or without the endpoints capability.</summary>
    /// <param name="id">The plugin id.</param>
    /// <param name="capabilities">The capabilities the manifest lists.</param>
    /// <returns>The loaded plugin, answered by <see cref="FulfilmentEndpointsPlugin"/>.</returns>
    private static LoadedPlugin Fulfilment(string id, params string[] capabilities)
    {
        return Fulfilment(id, typeof(FulfilmentEndpointsPlugin), capabilities);
    }

    /// <summary>A fulfilment plugin with one client page, answered by the given class.</summary>
    /// <param name="id">The plugin id.</param>
    /// <param name="implementation">The CLR type that answers.</param>
    /// <param name="capabilities">The capabilities the manifest lists.</param>
    /// <returns>The loaded plugin.</returns>
    private static LoadedPlugin Fulfilment(string id, Type implementation, params string[] capabilities)
    {
        var basis = PluginActivationTestSeeds.Plugin(id, PluginType.Fulfilment, implementation).Manifest;
        var manifest = new PluginManifest
        {
            ManifestVersion = 2,
            Id = basis.Id,
            Name = basis.Name,
            Version = basis.Version,
            Author = basis.Author,
            Description = basis.Description,
            Type = PluginType.Fulfilment,
            Category = basis.Category,
            EntryPoint = basis.EntryPoint,
            SdkVersion = basis.SdkVersion,
            Capabilities = capabilities,
            Ui = new PluginUi
            {
                Client = new PluginUiArea
                {
                    Pages = [new PluginPage { Id = PageId, Path = "/credits", Title = "Credits", View = $"views/{PageId}.json", Permission = PluginPermissions.Any }],
                },
            },
        };
        using var view = JsonDocument.Parse("""{"schema":1,"blocks":[]}""");
        var views = new Dictionary<string, JsonElement> { [$"views/{PageId}.json"] = view.RootElement.Clone() };
        return new LoadedPlugin(manifest, implementation, new PluginViewDocuments(views));
    }

    /// <summary>
    /// Derives a host whose registry holds three fulfilment plugins, all switched on: one serving, one without the
    /// capability and one declaring it on a class that implements only <c>IFulfilmentPlugin</c>.
    /// </summary>
    /// <param name="serving">The id of the plugin that declares endpoints and implements it.</param>
    /// <param name="plain">The id of the plugin that does not declare it.</param>
    /// <param name="broken">The id of the plugin that declares it without implementing it.</param>
    /// <returns>The host, to dispose when the test ends.</returns>
    private async Task<WebApplicationFactory<Program>> HostAsync(string serving, string plain, string broken)
    {
        var registry = PluginActivationTestSeeds.Registry(
            Fulfilment(serving, PluginCapabilities.Endpoints),
            Fulfilment(plain),
            Fulfilment(broken, typeof(ConfigurationCapturingFulfilmentPlugin), PluginCapabilities.Endpoints));
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            await PluginActivationTestSeeds.EnableAsync(
                scope.ServiceProvider.GetRequiredService<AppDbContext>(), serving, plain, broken);
        }

        return _fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<PluginRegistry>();
            services.AddSingleton(registry);
        }));
    }

    /// <summary>
    /// The account holder sees the serving fulfilment plugin's page in the index and is served it; a plugin without the
    /// capability, or with it declared but not implemented, is absent and answers the unknown-plugin 404, byte for byte.
    /// </summary>
    /// <param name="broken">Whether the refused plugin declares the capability without implementing it.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Client_FulfilmentWithEndpoints_IsListedAndServed_OneThatDoesNotServe_IsHidden(bool broken)
    {
        var serving = PluginActivationTestSeeds.NewId("ui-sell");
        var plainId = PluginActivationTestSeeds.NewId("ui-plain");
        var brokenId = PluginActivationTestSeeds.NewId("ui-broken");
        var plain = broken ? brokenId : plainId;
        using var host = await HostAsync(serving, plainId, brokenId);
        var owner = await ClientsTestSeeds.RegisterClientAsync(_fixture.Factory, "ui-capability");
        using var http = AppPluginFixture.WithBearerOf(host, owner.Client);

        using var indexResponse = await http.GetAsync(ClientIndexUrl);
        using var index = JsonDocument.Parse(await indexResponse.Content.ReadAsStringAsync());
        var pluginIds = index.RootElement.GetProperty("pages").EnumerateArray().Select(page => page.GetProperty("pluginId").GetString()).ToList();
        using var served = await http.GetAsync($"/api/client/plugins/ui/{serving}/pages/{PageId}");
        using var hidden = await http.GetAsync($"/api/client/plugins/ui/{plain}/pages/{PageId}");
        using var unknown = await http.GetAsync($"/api/client/plugins/ui/never-loaded/pages/{PageId}");

        indexResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        pluginIds.Should().Contain(serving).And.NotContain(plain);
        served.StatusCode.Should().Be(HttpStatusCode.OK);
        hidden.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(hidden, HostpanelErrorCodes.PLUGIN_PAGE_NOT_FOUND);
        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await hidden.Content.ReadAsStringAsync()).Should().Be(await unknown.Content.ReadAsStringAsync());
    }
}
