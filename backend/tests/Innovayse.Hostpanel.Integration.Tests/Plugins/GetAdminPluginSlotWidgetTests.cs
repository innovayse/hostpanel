using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Hostpanel.Sdk.Plugins.Manifest;
using Hostpanel.Sdk.Plugins.Slots;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Plugins;

/// <summary>
/// One admin slot widget's view (<c>GetAdminPluginSlotWidgetQuery</c> through <c>AdminPluginSlotsController</c>), against
/// Postgres and test-only app plugins: served with its labels in the request's language for a widget the admin index
/// would list for the same context, never by building the plugin; the context keeps the codes of the admin index, a widget
/// the index does not list is <c>PLUGIN_SLOT_WIDGET_VIEW_NOT_FOUND</c>, and only an administrator may ask.
/// </summary>
public sealed class GetAdminPluginSlotWidgetTests : IClassFixture<AppPluginFixture>
{
    /// <summary>A view document with one key-shaped text label, resolved through the manifest's locales.</summary>
    private const string ViewJson = """{"schema":1,"blocks":[{"kind":"text","text":"widget.hello"}]}""";

    /// <summary>The manifest texts the view's label resolves through, in the three languages.</summary>
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Locales =
        new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["en"] = new Dictionary<string, string> { ["widget.hello"] = "Hello" },
            ["ru"] = new Dictionary<string, string> { ["widget.hello"] = "Привет" },
            ["hy"] = new Dictionary<string, string> { ["widget.hello"] = "Բարեւ" },
        };

    /// <summary>The host, the database and the probe that counts plugin constructions.</summary>
    private readonly AppPluginFixture _fixture;

    /// <summary>Initializes the test, which starts from a probe that has seen nothing.</summary>
    /// <param name="fixture">The host, the database and the probe.</param>
    public GetAdminPluginSlotWidgetTests(AppPluginFixture fixture)
    {
        _fixture = fixture;
        _fixture.Probe.Reset();
    }

    /// <summary>The address of an admin slot widget's view.</summary>
    /// <param name="slot">The slot id.</param>
    /// <param name="pluginId">The plugin id.</param>
    /// <param name="widgetId">The widget id.</param>
    /// <param name="query">The query string, without the question mark; empty for none.</param>
    /// <returns>The relative URL.</returns>
    private static string Url(string slot, string pluginId, string widgetId, string query = "")
    {
        var path = $"/api/admin/plugins/slots/{slot}/widgets/{pluginId}/{widgetId}";
        return query.Length == 0 ? path : $"{path}?{query}";
    }

    /// <summary>A plugin with the given widgets and a view document for each, texts in en, ru and hy.</summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <param name="widgets">The widgets.</param>
    /// <returns>The loaded plugin.</returns>
    private static Infrastructure.Plugins.LoadedPlugin WithViews(string pluginId, params PluginSlotEntry[] widgets)
    {
        var plugin = PluginSlotSeeds.Plugin(pluginId, global::Hostpanel.Sdk.Plugins.Enums.PluginType.App, Locales, widgets);
        foreach (var widget in widgets)
        {
            plugin = PluginSlotSeeds.WithView(plugin, widget.View, ViewJson);
        }

        return plugin;
    }

    /// <summary>Reads the product a seeded service orders.</summary>
    /// <param name="serviceId">The service id.</param>
    /// <returns>The id of the product the service orders.</returns>
    private async Task<int> ProductOfAsync(int serviceId)
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await db.ClientServices.AsNoTracking().Where(s => s.Id == serviceId).Select(s => s.ProductId).SingleAsync();
    }

    /// <summary>A client's widget answers 200 with its view, labels in the request's language, and no plugin is built.</summary>
    /// <param name="language">The request's language.</param>
    /// <param name="expected">The resolved label.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData("en", "Hello")]
    [InlineData("ru", "Привет")]
    [InlineData("hy", "Բարեւ")]
    public async Task AWidgetOfAClientSlot_AnswersItsViewInTheRequestLanguageWithoutBuildingThePlugin(string language, string expected)
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(_fixture.Factory);
        var pluginId = PluginActivationTestSeeds.NewId("admin-widget");
        using var host = await PluginSlotSeeds.HostAsync(
            _fixture, [pluginId], WithViews(pluginId, PluginSlotSeeds.Widget("panel", PluginSlots.AdminClientPanels)));
        using var http = AppPluginFixture.WithBearerOf(host, await _fixture.AdminClientAsync());
        http.DefaultRequestHeaders.Add("X-Language", language);

        var response = await http.GetAsync(Url(PluginSlots.AdminClientPanels, pluginId, "panel", $"clientId={clientId}"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("pluginId").GetString().Should().Be(pluginId);
        body.GetProperty("id").GetString().Should().Be("panel");
        body.GetProperty("view").GetProperty("blocks")[0].GetProperty("text").GetString().Should().Be(expected);
        body.GetProperty("externalOrigins").GetArrayLength().Should().Be(0);
        _fixture.Probe.Constructed.Should().Be(0);
    }

    /// <summary>A service slot's widget is served only for the service's product, and the service and its client are verified.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AServiceSlotWidget_FollowsTheServicesProductAndVerifiesTheContext()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(_fixture.Factory);
        var service = await ServicesTestSeeds.SeedPendingServiceAsync(_fixture.Factory, clientId);
        var product = await ProductOfAsync(service);
        var pluginId = PluginActivationTestSeeds.NewId("admin-widget");
        var slot = PluginSlots.AdminServicePanels;
        using var host = await PluginSlotSeeds.HostAsync(
            _fixture,
            [pluginId],
            WithViews(
                pluginId,
                PluginSlotSeeds.Widget("this-product", slot, productIds: [product]),
                PluginSlotSeeds.Widget("other-product", slot, productIds: [int.MaxValue])));
        using var http = AppPluginFixture.WithBearerOf(host, await _fixture.AdminClientAsync());

        var served = await http.GetAsync(Url(slot, pluginId, "this-product", $"serviceId={service}&clientId={clientId}"));
        var otherProduct = await http.GetAsync(Url(slot, pluginId, "other-product", $"serviceId={service}"));
        var noService = await http.GetAsync(Url(slot, pluginId, "this-product"));
        var missingService = await http.GetAsync(Url(slot, pluginId, "this-product", $"serviceId={int.MaxValue}"));
        var foreignClient = await http.GetAsync(Url(slot, pluginId, "this-product", $"serviceId={service}&clientId={clientId + 1000000}"));

        served.StatusCode.Should().Be(HttpStatusCode.OK);
        otherProduct.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(otherProduct, HostpanelErrorCodes.PLUGIN_SLOT_WIDGET_VIEW_NOT_FOUND);
        noService.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await ClientsTestSeeds.AssertCodeAsync(noService, HostpanelErrorCodes.PLUGIN_SLOT_CONTEXT_REQUIRED);
        missingService.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(missingService, HostpanelErrorCodes.SERVICE_NOT_FOUND);
        foreignClient.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(foreignClient, HostpanelErrorCodes.SERVICE_NOT_FOUND);
        _fixture.Probe.Constructed.Should().Be(0);
    }

    /// <summary>
    /// An unknown slot, a client slot, a missing client, an unknown widget, a hidden one, a widget only another plugin declares, a
    /// switched-off plugin and a widget without a view each answer their own 404, and no plugin is built.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task EveryRefusal_IsA404WithItsOwnCode()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(_fixture.Factory);
        var shown = PluginActivationTestSeeds.NewId("admin-widget");
        var off = PluginActivationTestSeeds.NewId("admin-widget-off");
        var noView = PluginActivationTestSeeds.NewId("admin-widget-noview");
        var other = PluginActivationTestSeeds.NewId("admin-widget-other");
        var panels = PluginSlots.AdminClientPanels;
        using var host = await PluginSlotSeeds.HostAsync(
            _fixture,
            [shown, noView, other],
            WithViews(shown, PluginSlotSeeds.Widget("panel", panels), PluginSlotSeeds.Widget("hidden", panels)),
            WithViews(off, PluginSlotSeeds.Widget("panel", panels)),
            PluginSlotSeeds.Plugin(noView, PluginSlotSeeds.Widget("panel", panels)),
            WithViews(other, PluginSlotSeeds.Widget("only-other", panels)));
        using var http = AppPluginFixture.WithBearerOf(host, await _fixture.AdminClientAsync());
        await PluginSlotSeeds.HideAsync(_fixture.Factory, panels, shown, "hidden");
        var context = $"clientId={clientId}";

        var unknownSlot = await http.GetAsync(Url("admin.nothing.here", shown, "panel", context));
        var clientSlot = await http.GetAsync(Url(PluginSlots.ClientDashboardCards, shown, "panel"));
        var missingClient = await http.GetAsync(Url(panels, shown, "panel", $"clientId={int.MaxValue}"));
        var unknownWidget = await http.GetAsync(Url(panels, shown, "missing", context));
        var hidden = await http.GetAsync(Url(panels, shown, "hidden", context));
        var otherPluginsWidget = await http.GetAsync(Url(panels, shown, "only-other", context));
        var switchedOff = await http.GetAsync(Url(panels, off, "panel", context));
        var withoutView = await http.GetAsync(Url(panels, noView, "panel", context));

        await ClientsTestSeeds.AssertCodeAsync(unknownSlot, HostpanelErrorCodes.PLUGIN_SLOT_NOT_FOUND);
        await ClientsTestSeeds.AssertCodeAsync(clientSlot, HostpanelErrorCodes.PLUGIN_SLOT_NOT_FOUND);
        await ClientsTestSeeds.AssertCodeAsync(missingClient, HostpanelErrorCodes.CLIENT_NOT_FOUND);
        await ClientsTestSeeds.AssertCodeAsync(unknownWidget, HostpanelErrorCodes.PLUGIN_SLOT_WIDGET_VIEW_NOT_FOUND);
        await ClientsTestSeeds.AssertCodeAsync(hidden, HostpanelErrorCodes.PLUGIN_SLOT_WIDGET_VIEW_NOT_FOUND);
        await ClientsTestSeeds.AssertCodeAsync(otherPluginsWidget, HostpanelErrorCodes.PLUGIN_SLOT_WIDGET_VIEW_NOT_FOUND);
        await ClientsTestSeeds.AssertCodeAsync(switchedOff, HostpanelErrorCodes.PLUGIN_SLOT_WIDGET_VIEW_NOT_FOUND);
        await ClientsTestSeeds.AssertCodeAsync(withoutView, HostpanelErrorCodes.PLUGIN_SLOT_WIDGET_VIEW_NOT_FOUND);
        foreach (var answer in new[] { unknownSlot, clientSlot, missingClient, unknownWidget, hidden, otherPluginsWidget, switchedOff, withoutView })
        {
            answer.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        _fixture.Probe.Constructed.Should().Be(0);
    }

    /// <summary>A signed-in client user is refused, and no session at all is 401.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ANonAdministrator_IsRefused()
    {
        var client = await ClientsTestSeeds.RegisterClientAsync(_fixture.Factory, "slot-widget-admin-refused");
        using var asClient = _fixture.ClientLike(client.Client);
        using var anonymous = _fixture.Host.CreateClient();
        var url = Url(PluginSlots.AdminClientPanels, "any-plugin", "panel", $"clientId={client.ClientId}");

        var forbidden = await asClient.GetAsync(url);
        var signedOut = await anonymous.GetAsync(url);

        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        signedOut.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
