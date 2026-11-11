using System.Net;
using Hostpanel.Sdk.Plugins.Slots;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Plugins;

/// <summary>
/// The admin area slot index (<c>GetAdminPluginSlotQuery</c> through <c>AdminPluginSlotsController</c>), against Postgres
/// and test-only app plugins: the context must exist before anything is listed, no plugin is ever built, the product
/// binding follows the service the administrator opened, and only an administrator may ask.
/// </summary>
public sealed class GetAdminPluginSlotTests : IClassFixture<AppPluginFixture>
{
    /// <summary>The host, the database and the probe that counts plugin constructions.</summary>
    private readonly AppPluginFixture _fixture;

    /// <summary>Initializes the test, which starts from a probe that has seen nothing.</summary>
    /// <param name="fixture">The host, the database and the probe.</param>
    public GetAdminPluginSlotTests(AppPluginFixture fixture)
    {
        _fixture = fixture;
        _fixture.Probe.Reset();
    }

    /// <summary>The address of an admin slot index.</summary>
    /// <param name="slot">The slot id.</param>
    /// <param name="query">The query string, without the question mark; empty for none.</param>
    /// <returns>The relative URL.</returns>
    private static string Url(string slot, string query = "")
    {
        return query.Length == 0 ? $"/api/admin/plugins/slots/{slot}" : $"/api/admin/plugins/slots/{slot}?{query}";
    }

    /// <summary>Reads the client and the product of a seeded service.</summary>
    /// <param name="serviceId">The service id.</param>
    /// <returns>The id of the service's client and of the product it orders.</returns>
    private async Task<(int ClientId, int ProductId)> OwnerAndProductOfAsync(int serviceId)
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var row = await db.ClientServices.AsNoTracking()
            .Where(s => s.Id == serviceId)
            .Select(s => new { s.ClientId, s.ProductId })
            .SingleAsync();
        return (row.ClientId, row.ProductId);
    }

    /// <summary>A client that does not exist answers 404 <c>CLIENT_NOT_FOUND</c> and no plugin is built.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AnUnknownClient_Answers404AndNoPluginIsCalled()
    {
        var pluginId = PluginActivationTestSeeds.NewId("admin");
        using var host = await PluginSlotSeeds.HostAsync(
            _fixture, [pluginId], PluginSlotSeeds.Plugin(pluginId, PluginSlotSeeds.Widget("panel", PluginSlots.AdminClientPanels)));
        using var http = AppPluginFixture.WithBearerOf(host, await _fixture.AdminClientAsync());

        var response = await http.GetAsync(Url(PluginSlots.AdminClientPanels, $"clientId={int.MaxValue}"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_NOT_FOUND);
        _fixture.Probe.Constructed.Should().Be(0);
    }

    /// <summary>An existing client is served the widgets of the slot, without a plugin being built.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AnExistingClient_ListsTheWidgets()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(_fixture.Factory);
        var pluginId = PluginActivationTestSeeds.NewId("admin");
        var slot = PluginSlots.AdminClientPanels;
        using var host = await PluginSlotSeeds.HostAsync(
            _fixture, [pluginId], PluginSlotSeeds.Plugin(pluginId, PluginSlotSeeds.Widget("panel", slot)));
        using var http = AppPluginFixture.WithBearerOf(host, await _fixture.AdminClientAsync());

        var response = await http.GetAsync(Url(slot, $"clientId={clientId}"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await PluginSlotSeeds.WidgetsOfAsync(response)).Should().Equal($"{pluginId}/panel");
        _fixture.Probe.Constructed.Should().Be(0);
    }

    /// <summary>With no plugins the admin index is empty.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task WithNoPlugins_TheIndexIsEmpty()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(_fixture.Factory);
        using var host = await PluginSlotSeeds.HostAsync(_fixture, []);
        using var http = AppPluginFixture.WithBearerOf(host, await _fixture.AdminClientAsync());

        var response = await http.GetAsync(Url(PluginSlots.AdminClientPanels, $"clientId={clientId}"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await PluginSlotSeeds.WidgetsOfAsync(response)).Should().BeEmpty();
    }

    /// <summary>
    /// A service slot verifies the service exists and that the client named with it is its own, and a widget bound to the
    /// service's product is listed while one bound to another is not.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AServiceSlot_VerifiesTheServiceAndItsClientAndFiltersByProduct()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(_fixture.Factory);
        var service = await ServicesTestSeeds.SeedPendingServiceAsync(_fixture.Factory, clientId);
        var (ownerId, product) = await OwnerAndProductOfAsync(service);
        var pluginId = PluginActivationTestSeeds.NewId("admin");
        var slot = PluginSlots.AdminServicePanels;
        using var host = await PluginSlotSeeds.HostAsync(
            _fixture,
            [pluginId],
            PluginSlotSeeds.Plugin(
                pluginId,
                PluginSlotSeeds.Widget("this-product", slot, order: 1, productIds: [product]),
                PluginSlotSeeds.Widget("other-product", slot, order: 2, productIds: [int.MaxValue])));
        using var http = AppPluginFixture.WithBearerOf(host, await _fixture.AdminClientAsync());

        var listed = await http.GetAsync(Url(slot, $"serviceId={service}&clientId={ownerId}"));
        var unknownService = await http.GetAsync(Url(slot, $"serviceId={int.MaxValue}"));
        var otherClient = await http.GetAsync(Url(slot, $"serviceId={service}&clientId={ownerId + 1000000}"));

        listed.StatusCode.Should().Be(HttpStatusCode.OK);
        (await PluginSlotSeeds.WidgetsOfAsync(listed)).Should().Equal($"{pluginId}/this-product");
        unknownService.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(unknownService, HostpanelErrorCodes.SERVICE_NOT_FOUND);
        otherClient.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(otherClient, HostpanelErrorCodes.SERVICE_NOT_FOUND);
        _fixture.Probe.Constructed.Should().Be(0);
    }

    /// <summary>A hidden admin widget is absent.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AHiddenWidget_IsAbsent()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(_fixture.Factory);
        var pluginId = PluginActivationTestSeeds.NewId("admin");
        var slot = PluginSlots.AdminClientPanels;
        using var host = await PluginSlotSeeds.HostAsync(
            _fixture,
            [pluginId],
            PluginSlotSeeds.Plugin(pluginId, PluginSlotSeeds.Widget("hidden", slot), PluginSlotSeeds.Widget("shown", slot, order: 1)));
        using var http = AppPluginFixture.WithBearerOf(host, await _fixture.AdminClientAsync());
        await PluginSlotSeeds.HideAsync(_fixture.Factory, slot, pluginId, "hidden");

        var listed = await PluginSlotSeeds.WidgetsOfAsync(await http.GetAsync(Url(slot, $"clientId={clientId}")));

        listed.Should().Equal($"{pluginId}/shown");
    }

    /// <summary>A slot that needs a client or service and is asked without one answers 400, and a client slot answers 404 here.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AMissingContextIs400AndAClientSlotIs404()
    {
        using var http = await _fixture.AdminClientAsync();

        var noClient = await http.GetAsync(Url(PluginSlots.AdminClientPanels));
        var noService = await http.GetAsync(Url(PluginSlots.AdminServicePanels));
        var clientSlot = await http.GetAsync(Url(PluginSlots.ClientDashboardCards));

        noClient.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await ClientsTestSeeds.AssertCodeAsync(noClient, HostpanelErrorCodes.PLUGIN_SLOT_CONTEXT_REQUIRED);
        noService.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await ClientsTestSeeds.AssertCodeAsync(noService, HostpanelErrorCodes.PLUGIN_SLOT_CONTEXT_REQUIRED);
        clientSlot.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(clientSlot, HostpanelErrorCodes.PLUGIN_SLOT_NOT_FOUND);
    }

    /// <summary>A signed-in client user is refused the admin index.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AClientUser_IsRefused()
    {
        var client = await ClientsTestSeeds.RegisterClientAsync(_fixture.Factory, "slot-admin-refused");
        using var http = _fixture.ClientLike(client.Client);

        var response = await http.GetAsync(Url(PluginSlots.AdminClientPanels, $"clientId={client.ClientId}"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
