using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Plugins;
using Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;
using Innovayse.SDK.Plugins.Slots;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Plugins;

/// <summary>
/// <c>GET /api/admin/plugins/{id}/slot-widgets</c>: the operator lists the slot widgets a loaded plugin declares with the
/// products they are bound to next to the manifest products; an unknown plugin is 404 <c>PLUGIN_NOT_FOUND</c> and a
/// client user is closed out.
/// </summary>
public sealed class ListPluginSlotWidgetsTests : IClassFixture<AppPluginFixture>
{
    /// <summary>The host, the database and the probe that counts plugin constructions.</summary>
    private readonly AppPluginFixture _fixture;

    /// <summary>Initializes the test.</summary>
    /// <param name="fixture">The host, the database and the probe.</param>
    public ListPluginSlotWidgetsTests(AppPluginFixture fixture)
    {
        _fixture = fixture;
        _fixture.Probe.Reset();
    }

    /// <summary>A bound widget lists its binding next to the manifest products; an unbound one lists an empty binding.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ListsEachWidgetWithItsBindingAndItsManifestProducts()
    {
        var product = await OrdersTestSeeds.SeedProductAsync(_fixture.Factory);
        var pluginId = PluginActivationTestSeeds.NewId("bind");
        var slot = PluginSlots.AdminServicePanels;
        await BindAsync(pluginId, slot, "bound", product);
        using var host = await PluginSlotSeeds.HostAsync(
            _fixture,
            [pluginId],
            PluginSlotSeeds.Plugin(
                pluginId,
                PluginSlotSeeds.Widget("bound", slot, order: 1, productIds: [7]),
                PluginSlotSeeds.Widget("free", slot, order: 2)));
        using var http = AppPluginFixture.WithBearerOf(host, await _fixture.AdminClientAsync());

        var listed = (await http.GetFromJsonAsync<JsonElement[]>($"/api/admin/plugins/{pluginId}/slot-widgets"))!;

        listed.Should().HaveCount(2);
        var bound = listed.Single(w => w.GetProperty("widgetId").GetString() == "bound");
        bound.GetProperty("slotId").GetString().Should().Be(slot);
        bound.GetProperty("manifestProductIds").EnumerateArray().Select(id => id.GetInt32()).Should().Equal(7);
        bound.GetProperty("productIds").EnumerateArray().Select(id => id.GetInt32()).Should().Equal(product);
        bound.GetProperty("isBound").GetBoolean().Should().BeTrue();
        var free = listed.Single(w => w.GetProperty("widgetId").GetString() == "free");
        free.GetProperty("productIds").GetArrayLength().Should().Be(0);
    }

    /// <summary>
    /// A widget whose bound products were all deleted still lists as bound with no product, so the admin page can tell it
    /// from a widget never bound; the manifest binding does not come back.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AWidgetWhoseProductsWereDeleted_ListsAsBoundWithNoProducts()
    {
        var doomed = await OrdersTestSeeds.SeedProductAsync(_fixture.Factory);
        var pluginId = PluginActivationTestSeeds.NewId("bind");
        var slot = PluginSlots.AdminServicePanels;
        await BindAsync(pluginId, slot, "bound", doomed);
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            db.Products.Remove(await db.Products.SingleAsync(p => p.Id == doomed));
            await db.SaveChangesAsync(CancellationToken.None);
        }

        using var host = await PluginSlotSeeds.HostAsync(
            _fixture,
            [pluginId],
            PluginSlotSeeds.Plugin(
                pluginId, PluginSlotSeeds.Widget("bound", slot, order: 1, productIds: [7]), PluginSlotSeeds.Widget("free", slot, order: 2)));
        using var http = AppPluginFixture.WithBearerOf(host, await _fixture.AdminClientAsync());

        var listed = (await http.GetFromJsonAsync<JsonElement[]>($"/api/admin/plugins/{pluginId}/slot-widgets"))!;

        var bound = listed.Single(w => w.GetProperty("widgetId").GetString() == "bound");
        bound.GetProperty("isBound").GetBoolean().Should().BeTrue();
        bound.GetProperty("productIds").GetArrayLength().Should().Be(0);
        listed.Single(w => w.GetProperty("widgetId").GetString() == "free").GetProperty("isBound").GetBoolean().Should().BeFalse();
    }

    /// <summary>A plugin that is not loaded is 404 with its own code.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AnUnknownPlugin_IsNotFound()
    {
        var pluginId = PluginActivationTestSeeds.NewId("bind");
        using var host = await PluginSlotSeeds.HostAsync(
            _fixture, [pluginId], PluginSlotSeeds.Plugin(pluginId, PluginSlotSeeds.Widget("panel", PluginSlots.AdminServicePanels)));
        using var http = AppPluginFixture.WithBearerOf(host, await _fixture.AdminClientAsync());

        var response = await http.GetAsync("/api/admin/plugins/missing-plugin/slot-widgets");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_NOT_FOUND);
    }

    /// <summary>A client user may not list the bindings.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AClientUser_IsRefused()
    {
        var client = await ClientsTestSeeds.RegisterClientAsync(_fixture.Factory, "slot-list-refused");
        using var http = _fixture.ClientLike(client.Client);

        var response = await http.GetAsync("/api/admin/plugins/any-plugin/slot-widgets");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>Writes a binding row the way the operator command does.</summary>
    /// <param name="pluginId">The plugin.</param>
    /// <param name="slot">The slot id.</param>
    /// <param name="widgetId">The widget id.</param>
    /// <param name="productId">The product.</param>
    /// <returns>A task that completes when the row is saved.</returns>
    private async Task BindAsync(string pluginId, string slot, string widgetId, int productId)
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        db.PluginSlotProductBindings.Add(PluginSlotProductBinding.Create(pluginId, slot, widgetId, productId));
        await db.SaveChangesAsync(CancellationToken.None);
    }
}
