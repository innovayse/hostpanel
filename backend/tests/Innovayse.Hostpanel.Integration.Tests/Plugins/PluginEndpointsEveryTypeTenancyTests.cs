using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Slots;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

namespace Innovayse.Hostpanel.Integration.Tests.Plugins;

/// <summary>
/// A client reaches the service tab of its own service only, whatever the type of the plugin that serves it (#1357), against
/// Postgres and a real host: the slot index and the widget view asked for another client's service answer the very body a
/// service that does not exist gets, so a plugin tab never tells a client which services other clients hold.
/// </summary>
public sealed class PluginEndpointsEveryTypeTenancyTests : IClassFixture<AppPluginFixture>
{
    /// <summary>The host, the database and the plugins the derived hosts start from.</summary>
    private readonly AppPluginFixture _fixture;

    /// <summary>Initializes the test.</summary>
    /// <param name="fixture">The host, the database and the plugins the derived hosts start from.</param>
    public PluginEndpointsEveryTypeTenancyTests(AppPluginFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>The address of the service tab slot index for a service.</summary>
    /// <param name="serviceId">The service the page shows.</param>
    /// <returns>The relative URL.</returns>
    private static string IndexUrl(int serviceId)
    {
        return $"/api/client/plugins/slots/{PluginSlots.ClientServiceTabs}?serviceId={serviceId}";
    }

    /// <summary>The address of the view of a plugin's service tab for a service.</summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <param name="serviceId">The service the tab is opened for.</param>
    /// <returns>The relative URL.</returns>
    private static string ViewUrl(string pluginId, int serviceId)
    {
        return $"/api/client/plugins/slots/{PluginSlots.ClientServiceTabs}/widgets/{pluginId}/{PluginCapabilitySeeds.TabId}?serviceId={serviceId}";
    }

    /// <summary>
    /// Asked for another client's service, the slot index and the widget view of a serving plugin answer as for a service that
    /// does not exist.
    /// </summary>
    /// <param name="type">The plugin type.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [MemberData(nameof(ServingPluginTypes.All), MemberType = typeof(ServingPluginTypes))]
    public async Task AnotherClientsService_AnswersAsAMissingOne(PluginType type)
    {
        using var plugins = await ServingPluginHost.CreateAsync(_fixture, type);
        var caller = await ClientsTestSeeds.RegisterClientAsync(_fixture.Factory, "every-type-caller");
        var stranger = await ClientsTestSeeds.RegisterClientAsync(_fixture.Factory, "every-type-stranger");
        var foreign = await ServicesTestSeeds.SeedPendingServiceAsync(_fixture.Factory, stranger.ClientId);
        using var http = AppPluginFixture.WithBearerOf(plugins.Host, caller.Client);

        using var foreignIndex = await http.GetAsync(IndexUrl(foreign));
        using var missingIndex = await http.GetAsync(IndexUrl(int.MaxValue));
        using var foreignView = await http.GetAsync(ViewUrl(plugins.ServingId, foreign));
        using var missingView = await http.GetAsync(ViewUrl(plugins.ServingId, int.MaxValue));

        await PluginRefusalBodies.AssertSameAsync(foreignIndex, missingIndex, HostpanelErrorCodes.SERVICE_NOT_FOUND);
        await PluginRefusalBodies.AssertSameAsync(foreignView, missingView, HostpanelErrorCodes.PLUGIN_SLOT_WIDGET_VIEW_NOT_FOUND);
    }
}
