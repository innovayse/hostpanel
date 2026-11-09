using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Hostpanel.Sdk.Plugins.Slots;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Plugins;

/// <summary>
/// <c>PUT /api/admin/plugins/{id}/slot-widgets/{slotId}/{widgetId}</c>: the operator saves the products a slot widget is
/// bound to; unknown products are dropped, an empty list clears the binding, an unknown plugin or widget is refused with
/// its own code, a client user is closed out, and two first saves of one widget at once both succeed.
/// </summary>
public sealed class SavePluginSlotWidgetProductsTests : IClassFixture<AppPluginFixture>
{
    /// <summary>The host, the database and the probe that counts plugin constructions.</summary>
    private readonly AppPluginFixture _fixture;

    /// <summary>Initializes the test.</summary>
    /// <param name="fixture">The host, the database and the probe.</param>
    public SavePluginSlotWidgetProductsTests(AppPluginFixture fixture)
    {
        _fixture = fixture;
        _fixture.Probe.Reset();
    }

    /// <summary>
    /// Saving stores the existing products only and answers them, the list shows them next to the manifest products, and
    /// saving an empty list clears the binding.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Save_DropsUnknownProducts_AndAnEmptyListClearsTheBinding()
    {
        var product = await OrdersTestSeeds.SeedProductAsync(_fixture.Factory);
        var pluginId = PluginActivationTestSeeds.NewId("bind");
        var slot = PluginSlots.AdminServicePanels;
        using var host = await PluginSlotSeeds.HostAsync(
            _fixture, [pluginId], PluginSlotSeeds.Plugin(pluginId, PluginSlotSeeds.Widget("panel", slot, productIds: [7])));
        using var http = AppPluginFixture.WithBearerOf(host, await _fixture.AdminClientAsync());
        var url = $"/api/admin/plugins/{pluginId}/slot-widgets";

        var saved = await http.PutAsJsonAsync($"{url}/{slot}/panel", new { productIds = new[] { product, int.MaxValue } });
        var listed = await http.GetFromJsonAsync<JsonElement[]>(url);
        var cleared = await http.PutAsJsonAsync($"{url}/{slot}/panel", new { productIds = Array.Empty<int>() });
        var listedAfter = await http.GetFromJsonAsync<JsonElement[]>(url);

        saved.StatusCode.Should().Be(HttpStatusCode.OK);
        (await saved.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("productIds").EnumerateArray()
            .Select(id => id.GetInt32()).Should().Equal(product);
        listed.Should().ContainSingle();
        listed![0].GetProperty("slotId").GetString().Should().Be(slot);
        listed[0].GetProperty("widgetId").GetString().Should().Be("panel");
        listed[0].GetProperty("manifestProductIds").EnumerateArray().Select(id => id.GetInt32()).Should().Equal(7);
        listed[0].GetProperty("productIds").EnumerateArray().Select(id => id.GetInt32()).Should().Equal(product);
        cleared.StatusCode.Should().Be(HttpStatusCode.OK);
        listedAfter![0].GetProperty("productIds").GetArrayLength().Should().Be(0);
        (await RowCountAsync(pluginId)).Should().Be(0);
    }

    /// <summary>An unknown plugin and an undeclared widget are 404 with their own codes; a missing list is 400.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Save_UnknownPluginOrWidget_IsRefused()
    {
        var pluginId = PluginActivationTestSeeds.NewId("bind");
        var slot = PluginSlots.AdminServicePanels;
        using var host = await PluginSlotSeeds.HostAsync(
            _fixture, [pluginId], PluginSlotSeeds.Plugin(pluginId, PluginSlotSeeds.Widget("panel", slot)));
        using var http = AppPluginFixture.WithBearerOf(host, await _fixture.AdminClientAsync());

        var noPlugin = await http.PutAsJsonAsync($"/api/admin/plugins/missing-plugin/slot-widgets/{slot}/panel", new { productIds = new[] { 1 } });
        var noWidget = await http.PutAsJsonAsync($"/api/admin/plugins/{pluginId}/slot-widgets/{slot}/nope", new { productIds = new[] { 1 } });
        var noList = await http.PutAsJsonAsync($"/api/admin/plugins/{pluginId}/slot-widgets/{slot}/panel", new { });

        noPlugin.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(noPlugin, HostpanelErrorCodes.PLUGIN_NOT_FOUND);
        noWidget.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(noWidget, HostpanelErrorCodes.PLUGIN_SLOT_WIDGET_NOT_FOUND);
        noList.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>A client user may not save the bindings.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AClientUser_IsRefused()
    {
        var client = await ClientsTestSeeds.RegisterClientAsync(_fixture.Factory, "slot-binding-refused");
        using var http = _fixture.ClientLike(client.Client);

        var save = await http.PutAsJsonAsync("/api/admin/plugins/any-plugin/slot-widgets/s/w", new { productIds = new[] { 1 } });

        save.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// A competing save that commits the same first binding between this save's read and its write is refused by the
    /// unique index, mapped to a concurrency conflict, and the handler retries once from a fresh read: the save answers 200
    /// and exactly one row exists for the product.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task TwoFirstSavesOfOneWidget_BothSucceedAndLeaveOneRow()
    {
        var product = await OrdersTestSeeds.SeedProductAsync(_fixture.Factory);
        var pluginId = PluginActivationTestSeeds.NewId("bind");
        var slot = PluginSlots.AdminServicePanels;
        var race = new BindingRaceSaveInterceptor();
        using var plain = await PluginSlotSeeds.HostAsync(
            _fixture, [pluginId], PluginSlotSeeds.Plugin(pluginId, PluginSlotSeeds.Widget("panel", slot)));
        using var host = plain.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(race))));
        using var http = AppPluginFixture.WithBearerOf(host, await _fixture.AdminClientAsync());

        var response = await http.PutAsJsonAsync($"/api/admin/plugins/{pluginId}/slot-widgets/{slot}/panel", new { productIds = new[] { product } });

        race.Fired.Should().BeTrue();
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await RowCountAsync(pluginId)).Should().Be(1);
    }

    /// <summary>
    /// Products were sent and none exists: the save is refused with its own 400 code and the binding is left as it was;
    /// only an explicit empty list clears it.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Save_WhenEveryProductIsUnknown_IsRefusedAndKeepsTheBinding()
    {
        var product = await OrdersTestSeeds.SeedProductAsync(_fixture.Factory);
        var pluginId = PluginActivationTestSeeds.NewId("bind");
        var slot = PluginSlots.AdminServicePanels;
        using var host = await PluginSlotSeeds.HostAsync(
            _fixture, [pluginId], PluginSlotSeeds.Plugin(pluginId, PluginSlotSeeds.Widget("panel", slot)));
        using var http = AppPluginFixture.WithBearerOf(host, await _fixture.AdminClientAsync());
        var url = $"/api/admin/plugins/{pluginId}/slot-widgets/{slot}/panel";
        (await http.PutAsJsonAsync(url, new { productIds = new[] { product } })).StatusCode.Should().Be(HttpStatusCode.OK);

        var refused = await http.PutAsJsonAsync(url, new { productIds = new[] { int.MaxValue, int.MaxValue - 1 } });

        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await ClientsTestSeeds.AssertCodeAsync(refused, HostpanelErrorCodes.PLUGIN_SLOT_PRODUCTS_UNKNOWN);
        (await ProductsOfAsync(pluginId)).Should().Equal(product);
    }

    /// <summary>
    /// A competing save that wins the race on both attempts exhausts the retry: the save answers 409 and does not loop,
    /// and the interceptor fired exactly twice. Two products are sent so the second attempt still has a row to add.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Save_WhenTheRaceIsLostOnBothAttempts_AnswersConflictWithoutLooping()
    {
        var first = await OrdersTestSeeds.SeedProductAsync(_fixture.Factory);
        var second = await OrdersTestSeeds.SeedProductAsync(_fixture.Factory);
        var pluginId = PluginActivationTestSeeds.NewId("bind");
        var slot = PluginSlots.AdminServicePanels;
        var race = new BindingRaceSaveInterceptor(maxFires: int.MaxValue);
        using var plain = await PluginSlotSeeds.HostAsync(
            _fixture, [pluginId], PluginSlotSeeds.Plugin(pluginId, PluginSlotSeeds.Widget("panel", slot)));
        using var host = plain.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(race))));
        using var http = AppPluginFixture.WithBearerOf(host, await _fixture.AdminClientAsync());

        var response = await http.PutAsJsonAsync(
            $"/api/admin/plugins/{pluginId}/slot-widgets/{slot}/panel", new { productIds = new[] { first, second } });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        race.FireCount.Should().Be(2, "one attempt and one retry, then it gives up");
    }

    /// <summary>
    /// Two saves of disjoint sets, {first} and {second}, overlap on purpose: the first is held after it took the widget
    /// lock, the second starts and must wait for it. Once the first is let go the second replaces the whole set, so
    /// exactly one set remains and nothing is merged.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task TwoSavesOfDisjointSets_AreSerialisedAndTheLastWinsWhole()
    {
        var first = await OrdersTestSeeds.SeedProductAsync(_fixture.Factory);
        var second = await OrdersTestSeeds.SeedProductAsync(_fixture.Factory);
        var pluginId = PluginActivationTestSeeds.NewId("bind");
        var slot = PluginSlots.AdminServicePanels;
        var gate = new BindingGateSaveInterceptor();
        using var plain = await PluginSlotSeeds.HostAsync(
            _fixture, [pluginId], PluginSlotSeeds.Plugin(pluginId, PluginSlotSeeds.Widget("panel", slot)));
        using var gated = plain.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(gate))));
        using var httpFirst = AppPluginFixture.WithBearerOf(gated, await _fixture.AdminClientAsync());
        using var httpSecond = AppPluginFixture.WithBearerOf(plain, await _fixture.AdminClientAsync());
        var url = $"/api/admin/plugins/{pluginId}/slot-widgets/{slot}/panel";

        var firstSave = httpFirst.PutAsJsonAsync(url, new { productIds = new[] { first } });
        await gate.Entered.WaitAsync(TimeSpan.FromSeconds(60));
        var secondSave = httpSecond.PutAsJsonAsync(url, new { productIds = new[] { second } });
        await WaitForALockWaiterAsync();
        gate.Release();

        (await firstSave).StatusCode.Should().Be(HttpStatusCode.OK);
        (await secondSave).StatusCode.Should().Be(HttpStatusCode.OK);
        (await ProductsOfAsync(pluginId)).Should().Equal(second);
    }

    /// <summary>Waits until some session is blocked on an advisory lock, which is the second save waiting for the first.</summary>
    /// <returns>A task that completes once a waiter exists; fails the test after a minute without one.</returns>
    private async Task WaitForALockWaiterAsync()
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(60);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var scope = _fixture.Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var waiting = await db.Database
                .SqlQuery<int>($"SELECT COUNT(*)::int AS \"Value\" FROM pg_locks WHERE locktype = 'advisory' AND NOT granted")
                .SingleAsync();
            if (waiting > 0)
            {
                return;
            }

            await Task.Delay(20);
        }

        throw new TimeoutException("The second save never waited for the widget lock.");
    }

    /// <summary>Lists the products a plugin's stored bindings name.</summary>
    /// <param name="pluginId">The plugin.</param>
    /// <returns>The product ids, ordered.</returns>
    private async Task<IReadOnlyList<int>> ProductsOfAsync(string pluginId)
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await db.PluginSlotProductBindings.AsNoTracking()
            .Where(b => b.PluginId == pluginId)
            .OrderBy(b => b.ProductId)
            .Select(b => b.ProductId)
            .ToListAsync();
    }

    /// <summary>Counts the binding rows of a plugin.</summary>
    /// <param name="pluginId">The plugin.</param>
    /// <returns>The number of rows.</returns>
    private async Task<int> RowCountAsync(string pluginId)
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await db.PluginSlotProductBindings.AsNoTracking().CountAsync(b => b.PluginId == pluginId);
    }
}
