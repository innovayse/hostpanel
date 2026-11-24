using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Hostpanel.Sdk.Plugins.Manifest;
using Hostpanel.Sdk.Plugins.Slots;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;
using Innovayse.Hostpanel.Shared.Constants;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Plugins;

/// <summary>
/// <c>GET /api/admin/plugins/{id}/servers</c>: the operator reads the servers a loaded panel plugin serves, the ones stored on a server
/// module its manifest declares (matched ignoring case) and no others; a plugin that serves no server module answers empty lists; an
/// unknown plugin is 404 <c>PLUGIN_NOT_FOUND</c>, a malformed id is 400 and a client user is closed out.
/// </summary>
public sealed class ListPluginServersTests : IClassFixture<AppPluginFixture>
{
    /// <summary>The host, the database and the probe that counts plugin constructions.</summary>
    private readonly AppPluginFixture _fixture;

    /// <summary>Initializes the test.</summary>
    /// <param name="fixture">The host, the database and the probe.</param>
    public ListPluginServersTests(AppPluginFixture fixture)
    {
        _fixture = fixture;
        _fixture.Probe.Reset();
    }

    /// <summary>
    /// A panel plugin lists the servers on its modules only, whatever the letter case they are stored in, by name, and a server on
    /// another module is never listed.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ListsTheServersOfTheDeclaredModulesOnly_IgnoringCase_ByName()
    {
        var pluginId = PluginActivationTestSeeds.NewId("srv");
        var module = $"mod-{Guid.NewGuid():N}";
        var token = Guid.NewGuid().ToString("N");
        var second = await ServersTestSeeds.SeedServerAsync(_fixture.Factory, $"{token}-b", module.ToUpperInvariant());
        var first = await ServersTestSeeds.SeedServerAsync(_fixture.Factory, $"{token}-a", module);
        var other = await ServersTestSeeds.SeedServerAsync(_fixture.Factory, $"{token}-other", $"other-{Guid.NewGuid():N}");
        using var host = await HostAsync(PanelPlugin(pluginId, module), pluginId);
        using var http = AppPluginFixture.WithBearerOf(host, await _fixture.AdminClientAsync());

        var body = await http.GetFromJsonAsync<JsonElement>($"/api/admin/plugins/{pluginId}/servers");

        body.GetProperty("modules").EnumerateArray().Select(m => m.GetString()).Should().Equal(module);
        var ids = body.GetProperty("servers").EnumerateArray().Select(s => s.GetProperty("id").GetInt32()).ToList();
        ids.Should().Equal(first, second);
        ids.Should().NotContain(other);
    }

    /// <summary>A server never tested reads as unknown with no time, and one tested carries its outcome and the moment of the test.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task CarriesTheConnectionStateAndTheMomentOfTheLastTest()
    {
        var pluginId = PluginActivationTestSeeds.NewId("srv");
        var module = $"mod-{Guid.NewGuid():N}";
        var token = Guid.NewGuid().ToString("N");
        var tested = await ServersTestSeeds.SeedServerAsync(_fixture.Factory, $"{token}-a", module);
        var untested = await ServersTestSeeds.SeedServerAsync(_fixture.Factory, $"{token}-b", module);
        await ServersTestSeeds.RecordConnectionTestAsync(_fixture.Factory, tested, isOnline: false, accountsCount: null);
        using var host = await HostAsync(PanelPlugin(pluginId, module), pluginId);
        using var http = AppPluginFixture.WithBearerOf(host, await _fixture.AdminClientAsync());

        var body = await http.GetFromJsonAsync<JsonElement>($"/api/admin/plugins/{pluginId}/servers");

        var rows = body.GetProperty("servers").EnumerateArray().ToList();
        rows.Select(s => s.GetProperty("id").GetInt32()).Should().Equal(tested, untested);
        rows[0].GetProperty("isOnline").GetBoolean().Should().BeFalse();
        rows[0].GetProperty("lastTestedAt").GetDateTimeOffset().Should().Be(TestMoments.Seeded);
        rows[1].GetProperty("isOnline").ValueKind.Should().Be(JsonValueKind.Null);
        rows[1].GetProperty("lastTestedAt").ValueKind.Should().Be(JsonValueKind.Null);
    }

    /// <summary>A panel plugin no server is stored for answers its modules and an empty server list.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task APanelPluginWithNoServer_AnswersItsModulesAndNoServers()
    {
        var pluginId = PluginActivationTestSeeds.NewId("srv");
        var module = $"mod-{Guid.NewGuid():N}";
        using var host = await HostAsync(PanelPlugin(pluginId, module), pluginId);
        using var http = AppPluginFixture.WithBearerOf(host, await _fixture.AdminClientAsync());

        var body = await http.GetFromJsonAsync<JsonElement>($"/api/admin/plugins/{pluginId}/servers");

        body.GetProperty("modules").EnumerateArray().Select(m => m.GetString()).Should().Equal(module);
        body.GetProperty("servers").GetArrayLength().Should().Be(0);
    }

    /// <summary>
    /// A plugin that serves no server module (an app plugin, or a provisioning plugin that declares no provisioning capability even
    /// though its manifest lists modules) answers empty lists, and a server on the listed module stays out of it.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task APluginThatServesNoServerModule_AnswersEmptyLists()
    {
        var appId = PluginActivationTestSeeds.NewId("srv");
        var strayId = PluginActivationTestSeeds.NewId("srv");
        var stray = $"mod-{Guid.NewGuid():N}";
        await ServersTestSeeds.SeedServerAsync(_fixture.Factory, $"stray-{Guid.NewGuid():N}", stray);
        using var host = await PluginSlotSeeds.HostAsync(
            _fixture,
            [appId, strayId],
            PluginSlotSeeds.Plugin(appId, PluginSlotSeeds.Widget("panel", PluginSlots.AdminServicePanels)),
            PanelPluginHarness.Plugin(strayId, typeof(FakePanelPlugin), [stray], []));
        using var http = AppPluginFixture.WithBearerOf(host, await _fixture.AdminClientAsync());

        foreach (var id in new[] { appId, strayId })
        {
            var body = await http.GetFromJsonAsync<JsonElement>($"/api/admin/plugins/{id}/servers");

            body.GetProperty("modules").GetArrayLength().Should().Be(0);
            body.GetProperty("servers").GetArrayLength().Should().Be(0);
        }
    }

    /// <summary>A plugin that is not loaded is 404 with its own code.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AnUnknownPlugin_IsNotFound()
    {
        var pluginId = PluginActivationTestSeeds.NewId("srv");
        using var host = await HostAsync(PanelPlugin(pluginId, $"mod-{Guid.NewGuid():N}"), pluginId);
        using var http = AppPluginFixture.WithBearerOf(host, await _fixture.AdminClientAsync());

        var response = await http.GetAsync("/api/admin/plugins/missing-plugin/servers");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_NOT_FOUND);
    }

    /// <summary>An id that is not a well formed plugin id is refused with 400 before any plugin is looked up.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AMalformedPluginId_IsRefused()
    {
        using var http = await _fixture.AdminClientAsync();

        var response = await http.GetAsync("/api/admin/plugins/Not_A.Valid-Id/servers");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await PluginsTestSeeds.AssertCodeAsync(response, SharedErrorCodes.ValidationError);
    }

    /// <summary>A client user may not read the servers.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AClientUser_IsRefused()
    {
        var client = await ClientsTestSeeds.RegisterClientAsync(_fixture.Factory, "plugin-servers-refused");
        using var http = _fixture.ClientLike(client.Client);

        var response = await http.GetAsync("/api/admin/plugins/any-plugin/servers");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>Builds a panel plugin that declares one server module and the panel capability.</summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <param name="module">The server module the manifest declares.</param>
    /// <returns>The loaded plugin.</returns>
    private static LoadedPlugin PanelPlugin(string pluginId, string module)
    {
        return PanelPluginHarness.Plugin(pluginId, typeof(FakePanelPlugin), [module], [PluginCapabilities.ProvisioningPanel]);
    }

    /// <summary>Derives a host whose registry holds exactly the plugin, switched on.</summary>
    /// <param name="plugin">The plugin the registry holds.</param>
    /// <param name="enabledId">The id of the plugin to switch on.</param>
    /// <returns>The host, to dispose when the test ends.</returns>
    private Task<WebApplicationFactory<Program>> HostAsync(LoadedPlugin plugin, string enabledId)
    {
        return PluginSlotSeeds.HostAsync(_fixture, [enabledId], plugin);
    }
}
