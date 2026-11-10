using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Servers;

/// <summary>
/// <c>PUT /api/admin/servers/{id}</c>: creating then updating a server round-trips its new name,
/// an unknown id is refused <c>SERVER_NOT_FOUND</c>, a caller with only the Client role is
/// refused, and the route is closed to a caller with no credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpdateServerTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Creating then updating a server round-trips its new name.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CreateThenUpdate_RoundTripsAsync()
    {
        var client = await ServersTestSeeds.AdminClientAsync(factory);
        var name = $"Server-{Guid.NewGuid():N}";
        var id = await ServersTestSeeds.CreateServerAsync(client, name);
        var newName = $"{name}-renamed";

        var update = await client.PutAsJsonAsync(
            $"/api/admin/servers/{id}", ServersTestSeeds.ServerBody(newName, hostname: $"{name}.example.com", monthlyCost: 20m));
        update.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var list = await client.GetFromJsonAsync<List<System.Text.Json.JsonElement>>("/api/admin/servers");
        list!.Should().Contain(r => r.GetProperty("name").GetString() == newName);
    }

    /// <summary>
    /// The list says a credential is stored without returning it; an update with the field empty
    /// keeps it, and an explicit clear removes it.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Credentials_AreReportedKeptOnBlankAndRemovedOnClearAsync()
    {
        var client = await ServersTestSeeds.AdminClientAsync(factory);
        var name = $"Server-{Guid.NewGuid():N}";
        var id = await ServersTestSeeds.CreateServerAsync(client, name);

        var created = await ReadServerAsync(client, id);
        created.GetProperty("credentials").GetProperty("hasAccessHash").GetBoolean().Should().BeTrue();
        created.GetProperty("credentials").GetProperty("hasPassword").GetBoolean().Should().BeFalse();
        created.GetRawText().Should().NotContain("top-secret-access-hash");

        var blank = await client.PutAsJsonAsync($"/api/admin/servers/{id}", ServersTestSeeds.ServerBody(name, accessHash: string.Empty, monthlyCost: 12.5m, clearAccessHash: false));
        blank.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ReadServerAsync(client, id)).GetProperty("credentials").GetProperty("hasAccessHash").GetBoolean().Should().BeTrue();

        var cleared = await client.PutAsJsonAsync($"/api/admin/servers/{id}", ServersTestSeeds.ServerBody(name, accessHash: null, monthlyCost: 12.5m, clearAccessHash: true));
        cleared.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ReadServerAsync(client, id)).GetProperty("credentials").GetProperty("hasAccessHash").GetBoolean().Should().BeFalse();
    }

    /// <summary>Updating an unknown server id answers 404 with <c>SERVER_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UnknownId_RefusesNotFoundAsync()
    {
        var client = await ServersTestSeeds.AdminClientAsync(factory);

        var response = await client.PutAsJsonAsync(
            $"/api/admin/servers/{ServersTestSeeds.NoSuchId}",
            ServersTestSeeds.ServerBody("Ghost", hostname: "ghost.example.com"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ServersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.SERVER_NOT_FOUND);
    }

    /// <summary>A caller with only the Client role is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToTheClientRoleAsync()
    {
        var client = await ServersTestSeeds.ClientRoleClientAsync(factory, "update-server-forbidden");

        var response = await client.PutAsJsonAsync(
            $"/api/admin/servers/{ServersTestSeeds.NoSuchId}", new { name = "Ghost" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>The admin routes are not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().PutAsJsonAsync(
            $"/api/admin/servers/{ServersTestSeeds.NoSuchId}", new { name = "Ghost" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Reads one server from the admin list.</summary>
    /// <param name="client">Authenticated admin client.</param>
    /// <param name="id">The server's id.</param>
    /// <returns>The server's row.</returns>
    private static async Task<System.Text.Json.JsonElement> ReadServerAsync(HttpClient client, int id)
    {
        var list = await client.GetFromJsonAsync<List<System.Text.Json.JsonElement>>("/api/admin/servers");
        return list!.Single(r => r.GetProperty("id").GetInt32() == id);
    }
}
