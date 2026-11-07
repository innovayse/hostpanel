using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Plugins;
using Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;
using Hostpanel.Sdk.Plugins.Slots;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Plugins;

/// <summary>
/// The operator binding of a plugin slot widget to products (<c>PluginSlotProductBindings</c>): in the slot index it
/// overrides the manifest <c>productIds</c>, a binding whose products were all deleted shows nowhere, and a slot with
/// no service never shows a bound widget. Listing and saving the binding are covered by the tests of those use cases.
/// </summary>
public sealed class PluginSlotProductBindingTests : IClassFixture<AppPluginFixture>
{
    /// <summary>The host, the database and the probe that counts plugin constructions.</summary>
    private readonly AppPluginFixture _fixture;

    /// <summary>Initializes the test.</summary>
    /// <param name="fixture">The host, the database and the probe.</param>
    public PluginSlotProductBindingTests(AppPluginFixture fixture)
    {
        _fixture = fixture;
        _fixture.Probe.Reset();
    }

    /// <summary>A binding with products decides where the widget shows, whatever the manifest lists.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AnOperatorBinding_OverridesTheManifestProducts()
    {
        var (service, ownerId, product) = await SeedServiceAsync();
        var pluginId = PluginActivationTestSeeds.NewId("bind");
        var slot = PluginSlots.AdminServicePanels;
        await BindAsync(pluginId, slot, "panel", product);
        using var host = await PluginSlotSeeds.HostAsync(
            _fixture, [pluginId], PluginSlotSeeds.Plugin(pluginId, PluginSlotSeeds.Widget("panel", slot, productIds: [int.MaxValue])));
        using var http = AppPluginFixture.WithBearerOf(host, await _fixture.AdminClientAsync());

        var response = await http.GetAsync($"/api/admin/plugins/slots/{slot}?serviceId={service}&clientId={ownerId}");

        (await PluginSlotSeeds.WidgetsOfAsync(response)).Should().Equal($"{pluginId}/panel");
    }

    /// <summary>A binding that names another product hides a widget the manifest would have shown.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AnOperatorBinding_ToAnotherProduct_HidesTheWidgetTheManifestWouldShow()
    {
        var (service, ownerId, product) = await SeedServiceAsync();
        var other = await OrdersTestSeeds.SeedProductAsync(_fixture.Factory);
        var pluginId = PluginActivationTestSeeds.NewId("bind");
        var slot = PluginSlots.AdminServicePanels;
        await BindAsync(pluginId, slot, "panel", other);
        using var host = await PluginSlotSeeds.HostAsync(
            _fixture, [pluginId], PluginSlotSeeds.Plugin(pluginId, PluginSlotSeeds.Widget("panel", slot, productIds: [product])));
        using var http = AppPluginFixture.WithBearerOf(host, await _fixture.AdminClientAsync());

        var response = await http.GetAsync($"/api/admin/plugins/slots/{slot}?serviceId={service}&clientId={ownerId}");

        (await PluginSlotSeeds.WidgetsOfAsync(response)).Should().BeEmpty();
    }

    /// <summary>With no binding the manifest <c>productIds</c> apply as before.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task WithoutABinding_TheManifestProductsApply()
    {
        var (service, ownerId, product) = await SeedServiceAsync();
        var pluginId = PluginActivationTestSeeds.NewId("bind");
        var slot = PluginSlots.AdminServicePanels;
        using var host = await PluginSlotSeeds.HostAsync(
            _fixture,
            [pluginId],
            PluginSlotSeeds.Plugin(
                pluginId,
                PluginSlotSeeds.Widget("mine", slot, order: 1, productIds: [product]),
                PluginSlotSeeds.Widget("other", slot, order: 2, productIds: [int.MaxValue])));
        using var http = AppPluginFixture.WithBearerOf(host, await _fixture.AdminClientAsync());

        var response = await http.GetAsync($"/api/admin/plugins/slots/{slot}?serviceId={service}&clientId={ownerId}");

        (await PluginSlotSeeds.WidgetsOfAsync(response)).Should().Equal($"{pluginId}/mine");
    }

    /// <summary>A widget bound only to a product that was deleted shows nowhere: it does not fall back to its manifest.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ABindingToADeletedProduct_ShowsNowhereAndDoesNotFallBackToTheManifest()
    {
        var (service, ownerId, product) = await SeedServiceAsync();
        var doomed = await OrdersTestSeeds.SeedProductAsync(_fixture.Factory);
        var pluginId = PluginActivationTestSeeds.NewId("bind");
        var slot = PluginSlots.AdminServicePanels;
        await BindAsync(pluginId, slot, "panel", doomed);
        await DeleteProductAsync(doomed);
        using var host = await PluginSlotSeeds.HostAsync(
            _fixture, [pluginId], PluginSlotSeeds.Plugin(pluginId, PluginSlotSeeds.Widget("panel", slot, productIds: [product])));
        using var http = AppPluginFixture.WithBearerOf(host, await _fixture.AdminClientAsync());

        var response = await http.GetAsync($"/api/admin/plugins/slots/{slot}?serviceId={service}&clientId={ownerId}");

        (await PluginSlotSeeds.WidgetsOfAsync(response)).Should().BeEmpty("a bound widget whose products are gone must not widen back to the manifest");
    }

    /// <summary>A slot that has no service never shows a widget with a binding, but still shows one with none.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ASlotWithoutAService_NeverShowsABoundWidget()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(_fixture.Factory);
        var product = await OrdersTestSeeds.SeedProductAsync(_fixture.Factory);
        var pluginId = PluginActivationTestSeeds.NewId("bind");
        var slot = PluginSlots.AdminClientPanels;
        await BindAsync(pluginId, slot, "bound", product);
        using var host = await PluginSlotSeeds.HostAsync(
            _fixture,
            [pluginId],
            PluginSlotSeeds.Plugin(
                pluginId, PluginSlotSeeds.Widget("bound", slot, order: 1), PluginSlotSeeds.Widget("free", slot, order: 2)));
        using var http = AppPluginFixture.WithBearerOf(host, await _fixture.AdminClientAsync());

        var response = await http.GetAsync($"/api/admin/plugins/slots/{slot}?clientId={clientId}");

        (await PluginSlotSeeds.WidgetsOfAsync(response)).Should().Equal($"{pluginId}/free");
    }

    /// <summary>Seeds a pending service and reads its owner and product.</summary>
    /// <returns>The service id, its client id and its product id.</returns>
    private async Task<(int ServiceId, int ClientId, int ProductId)> SeedServiceAsync()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(_fixture.Factory);
        var service = await ServicesTestSeeds.SeedPendingServiceAsync(_fixture.Factory, clientId);
        using var scope = _fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var row = await db.ClientServices.AsNoTracking()
            .Where(s => s.Id == service)
            .Select(s => new { s.ClientId, s.ProductId })
            .SingleAsync();
        return (service, row.ClientId, row.ProductId);
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

    /// <summary>Deletes a product row.</summary>
    /// <param name="productId">The product.</param>
    /// <returns>A task that completes when the product is gone.</returns>
    private async Task DeleteProductAsync(int productId)
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        db.Products.Remove(await db.Products.SingleAsync(p => p.Id == productId));
        await db.SaveChangesAsync(CancellationToken.None);
    }
}
