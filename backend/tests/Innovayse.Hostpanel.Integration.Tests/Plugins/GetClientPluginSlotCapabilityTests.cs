using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;
using Innovayse.SDK.Plugins.Enums;
using Innovayse.SDK.Plugins.Manifest;
using Innovayse.SDK.Plugins.Slots;

namespace Innovayse.Hostpanel.Integration.Tests.Plugins;

/// <summary>
/// The client slot index (#644) with the <c>endpoints</c> capability (#818), against Postgres: a fulfilment plugin that
/// declares it puts its widget into a slot, the same plugin without it puts nothing there.
/// </summary>
public sealed class GetClientPluginSlotCapabilityTests : IClassFixture<AppPluginFixture>
{
    /// <summary>The host and the database the derived hosts start from.</summary>
    private readonly AppPluginFixture _fixture;

    /// <summary>Initializes the test.</summary>
    /// <param name="fixture">The host and the database the derived hosts start from.</param>
    public GetClientPluginSlotCapabilityTests(AppPluginFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>A fulfilment plugin with one dashboard widget, with or without the endpoints capability.</summary>
    /// <param name="id">The plugin id.</param>
    /// <param name="capabilities">The capabilities the manifest lists.</param>
    /// <returns>The loaded plugin.</returns>
    private static LoadedPlugin Fulfilment(string id, params string[] capabilities)
    {
        return Fulfilment(id, typeof(FulfilmentEndpointsPlugin), capabilities);
    }

    /// <summary>A fulfilment plugin with one dashboard widget, answered by the given class.</summary>
    /// <param name="id">The plugin id.</param>
    /// <param name="implementation">The CLR type that answers.</param>
    /// <param name="capabilities">The capabilities the manifest lists.</param>
    /// <returns>The loaded plugin.</returns>
    private static LoadedPlugin Fulfilment(string id, Type implementation, params string[] capabilities)
    {
        var basis = PluginSlotSeeds.Plugin(
            id, PluginType.Fulfilment, new Dictionary<string, IReadOnlyDictionary<string, string>>(),
            PluginSlotSeeds.Widget("credits", PluginSlots.ClientDashboardCards, permission: PluginPermissions.Any)).Manifest;
        var manifest = new PluginManifest
        {
            Id = basis.Id,
            Name = basis.Name,
            Version = basis.Version,
            Author = basis.Author,
            Description = basis.Description,
            Type = basis.Type,
            Category = basis.Category,
            EntryPoint = basis.EntryPoint,
            SdkVersion = basis.SdkVersion,
            Capabilities = capabilities,
            Ui = basis.Ui,
        };

        return new LoadedPlugin(manifest, implementation, PluginViewDocuments.None);
    }

    /// <summary>
    /// Only the fulfilment plugin that declares endpoints and implements them lists its widget in the dashboard slot;
    /// one without the capability, or declaring it on a class that does not implement it, lists nothing.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task FulfilmentWithEndpoints_ListsItsWidget_OnesThatDoNotServe_ListNothing()
    {
        var serving = PluginActivationTestSeeds.NewId("slot-sell");
        var plain = PluginActivationTestSeeds.NewId("slot-plain");
        var broken = PluginActivationTestSeeds.NewId("slot-broken");
        using var host = await PluginSlotSeeds.HostAsync(
            _fixture,
            [serving, plain, broken],
            Fulfilment(serving, PluginCapabilities.Endpoints),
            Fulfilment(plain),
            Fulfilment(broken, typeof(ConfigurationCapturingFulfilmentPlugin), PluginCapabilities.Endpoints));
        var owner = await ClientsTestSeeds.RegisterClientAsync(_fixture.Factory, "slot-capability");
        using var http = AppPluginFixture.WithBearerOf(host, owner.Client);

        using var response = await http.GetAsync($"/api/client/plugins/slots/{PluginSlots.ClientDashboardCards}");

        var widgets = await PluginSlotSeeds.WidgetsOfAsync(response);
        widgets.Should().Contain(widget => widget.StartsWith(serving, StringComparison.Ordinal))
            .And.NotContain(widget => widget.StartsWith(plain, StringComparison.Ordinal))
            .And.NotContain(widget => widget.StartsWith(broken, StringComparison.Ordinal));
    }
}
