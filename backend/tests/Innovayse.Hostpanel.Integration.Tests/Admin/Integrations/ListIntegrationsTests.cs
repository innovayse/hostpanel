using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Admin.Integrations.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.SDK.Plugins.Enums;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Integrations;

/// <summary>
/// <c>GET /api/admin/integrations</c>: an admin reads the enabled/configured status for every
/// known integration and plugin-provided one, a caller with only the Client role is refused, and
/// the route is closed to a caller with no credential. This use case refuses nothing once
/// authorized.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListIntegrationsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The list carries every built-in integration, the card gateway among them.</summary>
    [Fact]
    public async Task ShowsTheBuiltInIntegrationsAsync()
    {
        var client = await IntegrationsTestSeeds.AdminClientAsync(factory);

        var list = await client.GetFromJsonAsync<List<JsonElement>>("/api/admin/integrations");

        list!.Should().Contain(i => i.GetProperty("slug").GetString() == "stripe");
    }

    /// <summary>
    /// Once the card gateway's required secret key is stored, its row reports it as configured;
    /// the list carries no description and no last test moment, which nothing stores.
    /// </summary>
    [Fact]
    public async Task ReportsAnIntegrationWithItsRequiredFieldsAsConfiguredAsync()
    {
        var client = await IntegrationsTestSeeds.AdminClientAsync(factory);
        var save = await client.PutAsJsonAsync("/api/admin/integrations/stripe", new
        {
            isEnabled = false,
            config = new Dictionary<string, string> { ["secret_key"] = "sk_test_configured" },
        });
        save.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var list = await client.GetFromJsonAsync<List<JsonElement>>("/api/admin/integrations");

        var stripe = list!.Single(i => i.GetProperty("slug").GetString() == "stripe");
        stripe.GetProperty("isConfigured").GetBoolean().Should().BeTrue();
        stripe.TryGetProperty("description", out _).Should().BeFalse();
        stripe.TryGetProperty("lastTestedAt", out _).Should().BeFalse();
    }

    /// <summary>
    /// A plugin row names the plugin type its manifest declares, the fulfilment, identity and hosted types
    /// included, so the admin can group it; a built-in row names none.
    /// </summary>
    /// <param name="type">The plugin type the registered entry declares.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData(PluginType.Fulfilment)]
    [InlineData(PluginType.Identity)]
    [InlineData(PluginType.Hosted)]
    [InlineData(PluginType.Payment)]
    public async Task NamesThePluginTypeOfAPluginRowAsync(PluginType type)
    {
        var slug = $"plugin-{Guid.NewGuid():N}";
        var plugins = new PluginIntegrationRegistry();
        plugins.Register(new PluginIntegrationEntry(slug, "Test plugin", "Extensions", type, "#123456", []));
        var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IPluginIntegrationRegistry>(plugins)));
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await factory.GetAdminTokenAsync());

        var list = await client.GetFromJsonAsync<List<JsonElement>>("/api/admin/integrations");

        var row = list!.Single(i => i.GetProperty("slug").GetString() == slug);
        row.GetProperty("isPlugin").GetBoolean().Should().BeTrue();
        row.GetProperty("pluginType").GetString().Should().Be(type.ToString());
        var stripe = list!.Single(i => i.GetProperty("slug").GetString() == "stripe");
        stripe.GetProperty("pluginType").ValueKind.Should().Be(JsonValueKind.Null);
    }

    /// <summary>A caller with only the Client role is refused with 403.</summary>
    [Fact]
    public async Task IsClosedToTheClientRoleAsync()
    {
        var client = await IntegrationsTestSeeds.ClientRoleClientAsync(factory, "list-integrations-forbidden");

        var response = await client.GetAsync("/api/admin/integrations");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>The admin routes are not for anonymous callers.</summary>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().GetAsync("/api/admin/integrations");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
