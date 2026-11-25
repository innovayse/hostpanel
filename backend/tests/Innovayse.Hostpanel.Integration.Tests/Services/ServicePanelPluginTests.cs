using System.Text.Json;
using Hostpanel.Sdk.Plugins.Manifest;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Servers;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// The service reads name the panel plugin that serves the server's module (<c>server.panelPlugin</c>), for the admin detail and the
/// client's own list alike, and carry <c>null</c> when no plugin serves it, so the Open panel button shows the plugin's icon or the
/// generic one.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ServicePanelPluginTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A registry holding one panel plugin that serves the host's own module id, as an older stored server is.</summary>
    /// <returns>The registry.</returns>
    private static PluginRegistry RegistryWithPlugin()
    {
        return PluginActivationTestSeeds.Registry(
            PanelPluginHarness.Plugin("hestia", typeof(FakePanelPlugin), [ServerModules.CPanel], [PluginCapabilities.ProvisioningPanel]));
    }

    /// <summary>Builds a client over a host whose panel plugin reader looks at the given registry, signed in as the given caller.</summary>
    /// <param name="registry">The registry the reader reads.</param>
    /// <param name="caller">A client already signed in, whose authorization is copied.</param>
    /// <returns>The client.</returns>
    private HttpClient HostOver(PluginRegistry registry, HttpClient caller)
    {
        var reader = new ServerPanelPluginReader(new ServerModulePluginResolver(registry, NullLogger<ServerModulePluginResolver>.Instance), registry);
        var client = factory
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddSingleton<IServerPanelPluginReader>(reader)))
            .CreateClient();
        client.DefaultRequestHeaders.Authorization = caller.DefaultRequestHeaders.Authorization;
        return client;
    }

    /// <summary>Seeds a service of the client activated on a new server.</summary>
    /// <param name="clientId">The owning client.</param>
    /// <returns>The service id.</returns>
    private async Task<int> SeedServiceOnServerAsync(int clientId)
    {
        var serviceId = await ServicesTestSeeds.SeedPendingServiceAsync(factory, clientId);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var server = await ProvisioningTestSeeds.SeedServerAsync(context);
        var service = context.ClientServices.Single(s => s.Id == serviceId);
        service.ActivateOn(server.Id, "panel-plugin-ref", TestMoments.Seeded, TestMoments.Seeded);
        await context.SaveChangesAsync(CancellationToken.None);
        return serviceId;
    }

    /// <summary>The admin detail names the plugin that serves the server's module.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminDetail_NamesTheServingPluginAsync()
    {
        var owner = await ServicesTestSeeds.RegisterOwnerAsync(factory, "panel-plugin-admin");
        var serviceId = await SeedServiceOnServerAsync(owner.ClientId);
        var admin = HostOver(RegistryWithPlugin(), await ServicesTestSeeds.AdminClientAsync(factory));

        var response = await admin.GetAsync($"/api/services/{serviceId}");

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var plugin = body.RootElement.GetProperty("server").GetProperty("panelPlugin");
        plugin.GetProperty("id").GetString().Should().Be("hestia");
        plugin.GetProperty("name").GetString().Should().Be("Panel plugin");
        plugin.GetProperty("hasIcon").GetBoolean().Should().BeFalse();
    }

    /// <summary>The client's own list names the plugin that serves the server's module.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ClientList_NamesTheServingPluginAsync()
    {
        var owner = await ServicesTestSeeds.RegisterOwnerAsync(factory, "panel-plugin-client");
        var serviceId = await SeedServiceOnServerAsync(owner.ClientId);
        var client = HostOver(RegistryWithPlugin(), owner.Client);

        var response = await client.GetAsync("/api/me/services");

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var row = body.RootElement.EnumerateArray().Single(s => s.GetProperty("id").GetInt32() == serviceId);
        row.GetProperty("server").GetProperty("panelPlugin").GetProperty("id").GetString().Should().Be("hestia");
    }

    /// <summary>A server whose module no plugin serves (the host's own provider) carries a null plugin, so the screens fall back to the generic icon.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ClientList_ModuleNoPluginServes_HasNoPanelPluginAsync()
    {
        var owner = await ServicesTestSeeds.RegisterOwnerAsync(factory, "panel-plugin-none");
        var serviceId = await SeedServiceOnServerAsync(owner.ClientId);
        var client = HostOver(PluginActivationTestSeeds.Registry(), owner.Client);

        var response = await client.GetAsync("/api/me/services");

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var row = body.RootElement.EnumerateArray().Single(s => s.GetProperty("id").GetInt32() == serviceId);
        row.GetProperty("server").GetProperty("panelPlugin").ValueKind.Should().Be(JsonValueKind.Null);
    }
}
