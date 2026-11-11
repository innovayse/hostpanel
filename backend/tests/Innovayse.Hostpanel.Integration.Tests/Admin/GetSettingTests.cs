using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Admin;

/// <summary>
/// <c>GET /api/admin/settings/{id}</c>: an admin reads a single setting back by id, an id names
/// no setting is refused <c>SETTING_NOT_FOUND</c>, a caller with only the Client role is refused,
/// and the route is closed to a caller with no credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetSettingTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An id no seeded or created setting ever takes.</summary>
    private const int NoSuchSettingId = 999_999_999;

    /// <summary>An admin reads back a single seeded setting by id.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminCanReadASettingAsync()
    {
        var client = await AdminTestSeeds.AdminClientAsync(factory);
        var id = await AdminTestSeeds.FindSettingIdAsync(client, "portal.template");

        var response = await client.GetAsync($"/api/admin/settings/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("key").GetString().Should().Be("portal.template");
    }

    /// <summary>Reading an unknown id answers 404 with <c>SETTING_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UnknownId_RefusesNotFoundAsync()
    {
        var client = await AdminTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync($"/api/admin/settings/{NoSuchSettingId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await AdminTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.SETTING_NOT_FOUND);
    }

    /// <summary>A row outside the general catalogue answers 404 <c>SETTING_NOT_FOUND</c>, like an unknown id.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RowOutsideTheCatalogue_RefusesNotFoundAsync()
    {
        var id = await SecretSettingTestSeeds.StoreAsync(factory, "integration:gateway:publishable_key", "pk_test_read");
        var client = await AdminTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync($"/api/admin/settings/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await AdminTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.SETTING_NOT_FOUND);
    }

    /// <summary>A caller with only the Client role is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToTheClientRoleAsync()
    {
        var client = await AdminTestSeeds.ClientRoleClientAsync(factory, "get-setting-forbidden");

        var response = await client.GetAsync($"/api/admin/settings/{NoSuchSettingId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>A caller with no credential is refused with 401.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().GetAsync($"/api/admin/settings/{NoSuchSettingId}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
