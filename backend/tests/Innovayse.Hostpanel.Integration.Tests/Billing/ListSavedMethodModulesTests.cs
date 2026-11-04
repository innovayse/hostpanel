using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.SDK.Plugins.Enums;
using Innovayse.SDK.Plugins.Manifest;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>GET /api/me/payment-methods/modules</c> against Postgres: the payment modules a client can save a card with
/// are the loaded payment plugins whose saved-method capability is offered; a module the capability does not serve
/// is left out.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListSavedMethodModulesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The route under test.</summary>
    private const string Route = "/api/me/payment-methods/modules";

    /// <summary>A loaded payment plugin whose saved-method capability is not served.</summary>
    private const string UnservedModule = "unserved-gateway";

    /// <summary>Builds a payment-plugin manifest for a test plugin.</summary>
    /// <param name="id">The module id.</param>
    /// <param name="name">The display name.</param>
    /// <returns>The manifest.</returns>
    private static PluginManifest PaymentManifest(string id, string name) => new()
    {
        Id = id,
        Name = name,
        Version = "1.0.0",
        Author = "test",
        Description = "test",
        Type = PluginType.Payment,
        Category = "Payment Gateways",
        EntryPoint = "X",
        SdkVersion = "1.0",
    };

    /// <summary>Builds a host whose loaded plugins are the given manifests and whose capability is served for the fake module.</summary>
    /// <param name="gateway">The fake capability host.</param>
    /// <param name="plugins">The loaded plugin manifests.</param>
    /// <returns>The authenticated client of a new account on that host.</returns>
    private async Task<HttpClient> ClientOnAsync(StoredCredentialHost gateway, FakePluginRegistry plugins)
    {
        var host = gateway.Host.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IPluginRegistry>(plugins)));
        var (client, _) = await BillingTestSeeds.RegisterClientAsync(factory, host, "saved-modules");
        return client;
    }

    /// <summary>A loaded payment plugin that serves the capability is listed with its name.</summary>
    [Fact]
    public async Task List_ListsALoadedPluginThatServesTheCapability()
    {
        var plugins = new FakePluginRegistry();
        plugins.Add(PaymentManifest(StoredCredentialHost.Module, "Fake Bank"));
        var client = await ClientOnAsync(StoredCredentialHost.Create(factory), plugins);

        var response = await client.GetAsync(Route);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var modules = await response.Content.ReadFromJsonAsync<List<SavedMethodModuleDto>>();
        modules.Should().ContainSingle(m => m.Module == StoredCredentialHost.Module && m.DisplayName == "Bank Card (Fake Bank)");
    }

    /// <summary>A loaded payment plugin without the capability, and a capability switched off, are not listed.</summary>
    [Fact]
    public async Task List_LeavesOutModulesThatDoNotServeTheCapability()
    {
        var gateway = StoredCredentialHost.Create(factory);
        var plugins = new FakePluginRegistry();
        plugins.Add(PaymentManifest(StoredCredentialHost.Module, "Fake Bank"));
        plugins.Add(PaymentManifest(UnservedModule, "Other Bank"));
        var client = await ClientOnAsync(gateway, plugins);

        var served = await client.GetFromJsonAsync<List<SavedMethodModuleDto>>(Route);
        gateway.Resolver.Offered = false;
        var switchedOff = await client.GetFromJsonAsync<List<SavedMethodModuleDto>>(Route);

        served.Should().ContainSingle().Which.Module.Should().Be(StoredCredentialHost.Module);
        switchedOff.Should().NotContain(m => m.Module == StoredCredentialHost.Module || m.Module == UnservedModule);
    }

    /// <summary>The list is not for anonymous callers.</summary>
    [Fact]
    public async Task ListRefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync(Route);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
