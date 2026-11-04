using System.Net;
using System.Text.Json;
using Innovayse.SDK.Plugins.Enums;
using Innovayse.SDK.Plugins.Slots;

namespace Innovayse.Hostpanel.Integration.Tests.Plugins;

/// <summary>
/// A host with no plugin installed offers no plugin interface at all: every client and admin slot index and both
/// plugin ui indexes (pages and menus) are empty lists answered with 200, so the panels render exactly what the core
/// itself provides. Runs against Postgres and <see cref="EmptyPluginUiFixture"/>, whose registry holds no plugin.
/// </summary>
public sealed class EmptyPluginsTests : IClassFixture<EmptyPluginUiFixture>
{
    /// <summary>The host with an empty registry.</summary>
    private readonly EmptyPluginUiFixture _empty;

    /// <summary>Initializes the test.</summary>
    /// <param name="empty">The host with an empty registry.</param>
    public EmptyPluginsTests(EmptyPluginUiFixture empty)
    {
        _empty = empty;
    }

    /// <summary>Reads the length of one list member of an answered document.</summary>
    /// <param name="response">The answer, expected to be 200.</param>
    /// <param name="member">The member to read: <c>widgets</c>, <c>menus</c> or <c>pages</c>.</param>
    /// <returns>The number of elements in the list.</returns>
    private static async Task<int> CountAsync(HttpResponseMessage response, string member)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty(member).GetArrayLength();
    }

    /// <summary>Every client slot, including the one that needs a service, answers an empty widget list.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ClientSlots_AreEmpty()
    {
        var owner = await ClientsTestSeeds.RegisterClientAsync(_empty.Factory, "empty-client-slots");
        var serviceId = await ServicesTestSeeds.SeedPendingServiceAsync(_empty.Factory, owner.ClientId);
        using var http = AppPluginFixture.WithBearerOf(_empty.Host, owner.Client);

        foreach (var slot in PluginSlots.All.Where(slot => slot.Area == PluginArea.Client))
        {
            var url = $"/api/client/plugins/slots/{slot.Id}?serviceId={serviceId}";
            (await CountAsync(await http.GetAsync(url), "widgets")).Should().Be(0, $"slot {slot.Id} lists nothing without plugins");
        }
    }

    /// <summary>Every admin slot answers an empty widget list for an existing client and service.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AdminSlots_AreEmpty()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(_empty.Factory);
        var serviceId = await ServicesTestSeeds.SeedPendingServiceAsync(_empty.Factory, clientId);
        using var http = await PluginUiFixture.AdminClientOfAsync(_empty.Host, _empty.Factory);

        foreach (var slot in PluginSlots.All.Where(slot => slot.Area == PluginArea.Admin))
        {
            var url = $"/api/admin/plugins/slots/{slot.Id}?clientId={clientId}&serviceId={serviceId}";
            (await CountAsync(await http.GetAsync(url), "widgets")).Should().Be(0, $"slot {slot.Id} lists nothing without plugins");
        }
    }

    /// <summary>The client and admin plugin ui indexes list no page and no menu entry, so no panel gains a route or a menu item.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task UiIndexes_HaveNoMenusAndNoPages()
    {
        var owner = await ClientsTestSeeds.RegisterClientAsync(_empty.Factory, "empty-ui-indexes");
        using var client = AppPluginFixture.WithBearerOf(_empty.Host, owner.Client);
        using var admin = await PluginUiFixture.AdminClientOfAsync(_empty.Host, _empty.Factory);

        foreach (var (http, url) in new[] { (client, "/api/client/plugins/ui"), (admin, "/api/admin/plugins/ui") })
        {
            (await CountAsync(await http.GetAsync(url), "menus")).Should().Be(0, $"{url} lists no menu entry");
            (await CountAsync(await http.GetAsync(url), "pages")).Should().Be(0, $"{url} lists no page");
        }
    }
}
