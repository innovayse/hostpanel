using System.Net;
using System.Text.Json;
using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Slots;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

namespace Innovayse.Hostpanel.Integration.Tests.Plugins;

/// <summary>
/// The pages, menus and slot widgets of a plugin with the <c>endpoints</c> capability, once for every plugin type that can
/// declare it (#643, #644, #818, #1357), against Postgres and a real host: the indexes list a serving plugin's menu, page and
/// widgets and the page, path and widget view lookups serve them, while a plugin without the capability, or with it declared
/// on a class that does not implement it, is listed nowhere and every lookup answers the body of an unknown plugin id.
/// </summary>
public sealed class PluginEndpointsEveryTypeUiTests : IClassFixture<AppPluginFixture>
{
    /// <summary>A plugin id no host ever loads.</summary>
    private const string NeverLoaded = "never-loaded";

    /// <summary>The host, the database and the plugins the derived hosts start from.</summary>
    private readonly AppPluginFixture _fixture;

    /// <summary>Initializes the test.</summary>
    /// <param name="fixture">The host, the database and the plugins the derived hosts start from.</param>
    public PluginEndpointsEveryTypeUiTests(AppPluginFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>Reads the ids of the plugins that put an entry into one list of the ui index.</summary>
    /// <param name="index">The answered index.</param>
    /// <param name="list">The list: <c>menus</c> or <c>pages</c>.</param>
    /// <returns>The plugin id of every entry.</returns>
    private static IReadOnlyList<string?> PluginIdsOf(JsonDocument index, string list)
    {
        return index.RootElement.GetProperty(list).EnumerateArray().Select(entry => entry.GetProperty("pluginId").GetString()).ToList();
    }

    /// <summary>The address of a page found by its id.</summary>
    /// <param name="area">The area segment of the address: <c>client</c> or <c>admin</c>.</param>
    /// <param name="pluginId">The plugin id.</param>
    /// <returns>The relative URL.</returns>
    private static string PageUrl(string area, string pluginId)
    {
        return $"/api/{area}/plugins/ui/{pluginId}/pages/{PluginCapabilitySeeds.PageId}";
    }

    /// <summary>The address of a page found by its path.</summary>
    /// <param name="area">The area segment of the address: <c>client</c> or <c>admin</c>.</param>
    /// <param name="pluginId">The plugin id.</param>
    /// <returns>The relative URL.</returns>
    private static string PathUrl(string area, string pluginId)
    {
        return $"/api/{area}/plugins/ui/{pluginId}/pages/by-path?path={Uri.EscapeDataString(PluginCapabilitySeeds.PagePath)}";
    }

    /// <summary>The address of a service tab widget's view.</summary>
    /// <param name="serviceId">The service the tab is opened for.</param>
    /// <param name="pluginId">The plugin id.</param>
    /// <returns>The relative URL.</returns>
    private static string WidgetViewUrl(int serviceId, string pluginId)
    {
        return $"/api/client/plugins/slots/{PluginSlots.ClientServiceTabs}/widgets/{pluginId}/{PluginCapabilitySeeds.TabId}?serviceId={serviceId}";
    }

    /// <summary>Asserts that one area lists and serves the menu and the page of the serving plugin only, and answers for the others as for an unknown id.</summary>
    /// <param name="http">A caller of the area.</param>
    /// <param name="area">The area segment of the address: <c>client</c> or <c>admin</c>.</param>
    /// <param name="plugins">The host and the three plugins.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    private static async Task AssertPagesFollowTheCapabilityAsync(HttpClient http, string area, ServingPluginHost plugins)
    {
        using var indexResponse = await http.GetAsync($"/api/{area}/plugins/ui");
        using var index = JsonDocument.Parse(await indexResponse.Content.ReadAsStringAsync());
        using var servedById = await http.GetAsync(PageUrl(area, plugins.ServingId));
        using var servedByPath = await http.GetAsync(PathUrl(area, plugins.ServingId));
        using var unknownById = await http.GetAsync(PageUrl(area, NeverLoaded));
        using var unknownByPath = await http.GetAsync(PathUrl(area, NeverLoaded));

        indexResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        foreach (var list in new[] { "menus", "pages" })
        {
            PluginIdsOf(index, list).Should().Contain(plugins.ServingId)
                .And.NotContain(plugins.PlainId)
                .And.NotContain(plugins.BrokenId);
        }

        servedById.StatusCode.Should().Be(HttpStatusCode.OK);
        servedByPath.StatusCode.Should().Be(HttpStatusCode.OK);
        foreach (var refusedId in plugins.RefusedIds)
        {
            using var byId = await http.GetAsync(PageUrl(area, refusedId));
            using var byPath = await http.GetAsync(PathUrl(area, refusedId));

            await PluginRefusalBodies.AssertSameAsync(byId, unknownById, HostpanelErrorCodes.PLUGIN_PAGE_NOT_FOUND);
            await PluginRefusalBodies.AssertSameAsync(byPath, unknownByPath, HostpanelErrorCodes.PLUGIN_PAGE_NOT_FOUND);
        }
    }

    /// <summary>The account holder's menu and page of a plugin of any type are listed and served only when the plugin serves.</summary>
    /// <param name="type">The plugin type.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [MemberData(nameof(ServingPluginTypes.All), MemberType = typeof(ServingPluginTypes))]
    public async Task Client_PagesAndMenus_FollowTheCapability(PluginType type)
    {
        using var plugins = await ServingPluginHost.CreateAsync(_fixture, type);
        var owner = await ClientsTestSeeds.RegisterClientAsync(_fixture.Factory, "every-type-pages");
        using var http = AppPluginFixture.WithBearerOf(plugins.Host, owner.Client);

        await AssertPagesFollowTheCapabilityAsync(http, "client", plugins);
    }

    /// <summary>The administrator's menu and page of a plugin of any type are listed and served only when the plugin serves.</summary>
    /// <param name="type">The plugin type.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [MemberData(nameof(ServingPluginTypes.All), MemberType = typeof(ServingPluginTypes))]
    public async Task Admin_PagesAndMenus_FollowTheCapability(PluginType type)
    {
        using var plugins = await ServingPluginHost.CreateAsync(_fixture, type);
        using var http = AppPluginFixture.WithBearerOf(plugins.Host, await _fixture.AdminClientAsync());

        await AssertPagesFollowTheCapabilityAsync(http, "admin", plugins);
    }

    /// <summary>
    /// The service tab of a plugin of any type is listed in the slot index of the client's own service and its view is served
    /// only when the plugin serves; the others answer the one widget view 404 of an unknown plugin id.
    /// </summary>
    /// <param name="type">The plugin type.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [MemberData(nameof(ServingPluginTypes.All), MemberType = typeof(ServingPluginTypes))]
    public async Task Client_ServiceTab_FollowsTheCapability(PluginType type)
    {
        using var plugins = await ServingPluginHost.CreateAsync(_fixture, type);
        var owner = await ClientsTestSeeds.RegisterClientAsync(_fixture.Factory, "every-type-tab");
        var service = await ServicesTestSeeds.SeedPendingServiceAsync(_fixture.Factory, owner.ClientId);
        using var http = AppPluginFixture.WithBearerOf(plugins.Host, owner.Client);
        var slot = PluginSlots.ClientServiceTabs;

        using var indexResponse = await http.GetAsync($"/api/client/plugins/slots/{slot}?serviceId={service}");
        using var served = await http.GetAsync(WidgetViewUrl(service, plugins.ServingId));
        using var unknown = await http.GetAsync(WidgetViewUrl(service, NeverLoaded));

        indexResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await PluginSlotSeeds.WidgetsOfAsync(indexResponse)).Should().Equal($"{plugins.ServingId}/{PluginCapabilitySeeds.TabId}");
        served.StatusCode.Should().Be(HttpStatusCode.OK);
        foreach (var refusedId in plugins.RefusedIds)
        {
            using var refused = await http.GetAsync(WidgetViewUrl(service, refusedId));

            await PluginRefusalBodies.AssertSameAsync(refused, unknown, HostpanelErrorCodes.PLUGIN_SLOT_WIDGET_VIEW_NOT_FOUND);
        }
    }

    /// <summary>The service panel of a plugin of any type is listed in the administrator's slot index only when the plugin serves.</summary>
    /// <param name="type">The plugin type.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [MemberData(nameof(ServingPluginTypes.All), MemberType = typeof(ServingPluginTypes))]
    public async Task Admin_ServicePanel_FollowsTheCapability(PluginType type)
    {
        using var plugins = await ServingPluginHost.CreateAsync(_fixture, type);
        var owner = await ClientsTestSeeds.RegisterClientAsync(_fixture.Factory, "every-type-panel");
        var service = await ServicesTestSeeds.SeedPendingServiceAsync(_fixture.Factory, owner.ClientId);
        using var http = AppPluginFixture.WithBearerOf(plugins.Host, await _fixture.AdminClientAsync());

        using var response = await http.GetAsync(
            $"/api/admin/plugins/slots/{PluginSlots.AdminServicePanels}?serviceId={service}&clientId={owner.ClientId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await PluginSlotSeeds.WidgetsOfAsync(response)).Should().Equal($"{plugins.ServingId}/{PluginCapabilitySeeds.PanelId}");
    }
}
