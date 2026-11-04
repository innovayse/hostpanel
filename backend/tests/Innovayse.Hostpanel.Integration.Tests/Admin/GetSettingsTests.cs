using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Innovayse.Hostpanel.Integration.Tests.Admin;

/// <summary>
/// <c>GET /api/admin/settings</c>: an admin reads back every configured setting, a caller with
/// only the Client role is refused, and the route is closed to a caller with no credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetSettingsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The seeded storefront settings come back, in full, for an admin.</summary>
    [Fact]
    public async Task ReturnsTheSeededSettingsAsync()
    {
        var client = await AdminTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/admin/settings");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var rows = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
        rows!.Should().Contain(r => r.GetProperty("key").GetString() == "portal.template");
    }

    /// <summary>A row outside the general catalogue (an integration field) is not listed.</summary>
    [Fact]
    public async Task OmitsRowsOutsideTheCatalogueAsync()
    {
        await SecretSettingTestSeeds.StoreAsync(factory, "integration:stripe:publishable_key", "pk_test_listed");
        var client = await AdminTestSeeds.AdminClientAsync(factory);

        var rows = await client.GetFromJsonAsync<List<JsonElement>>("/api/admin/settings");

        rows!.Should().NotContain(r => r.GetProperty("key").GetString() == "integration:stripe:publishable_key");
    }

    /// <summary>A caller with only the Client role is refused with 403.</summary>
    [Fact]
    public async Task IsClosedToTheClientRoleAsync()
    {
        var client = await AdminTestSeeds.ClientRoleClientAsync(factory, "get-settings-forbidden");

        var response = await client.GetAsync("/api/admin/settings");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>A caller with no credential is refused with 401.</summary>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().GetAsync("/api/admin/settings");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
