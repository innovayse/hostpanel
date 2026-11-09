using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Servers.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Servers;

/// <summary>
/// <c>PUT /api/admin/server-groups/{id}</c>: updating a group's membership to drop a server
/// clears that server's group assignment, an unknown id is refused <c>SERVER_GROUP_NOT_FOUND</c>,
/// a caller with only the Client role is refused, and the route is closed to a caller with no
/// credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpdateServerGroupTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// Updating a group's membership to drop a server clears that server's group assignment.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RemovingAServer_ClearsItsGroupAsync()
    {
        var client = await ServersTestSeeds.AdminClientAsync(factory);
        var serverId = await ServersTestSeeds.CreateServerAsync(client, $"GroupServer-{Guid.NewGuid():N}");
        var groupName = $"Group-{Guid.NewGuid():N}";
        var groupId = await ServersTestSeeds.CreateServerGroupAsync(client, groupName, serverId);

        var update = await client.PutAsJsonAsync($"/api/admin/server-groups/{groupId}", new
        {
            name = groupName,
            fillType = ServerFillType.FillUntilFull,
            serverIds = Array.Empty<int>(),
        });
        update.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var servers = await client.GetFromJsonAsync<List<System.Text.Json.JsonElement>>("/api/admin/servers");
        var server = servers!.Single(s => s.GetProperty("id").GetInt32() == serverId);
        server.GetProperty("serverGroupId").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
    }

    /// <summary>Updating an unknown group id answers 404 with <c>SERVER_GROUP_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UnknownId_RefusesNotFoundAsync()
    {
        var client = await ServersTestSeeds.AdminClientAsync(factory);

        var response = await client.PutAsJsonAsync($"/api/admin/server-groups/{ServersTestSeeds.NoSuchId}", new
        {
            name = "Ghost",
            fillType = ServerFillType.LeastFull,
            serverIds = Array.Empty<int>(),
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ServersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.SERVER_GROUP_NOT_FOUND);
    }

    /// <summary>A caller with only the Client role is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToTheClientRoleAsync()
    {
        var client = await ServersTestSeeds.ClientRoleClientAsync(factory, "update-server-group-forbidden");

        var response = await client.PutAsJsonAsync(
            $"/api/admin/server-groups/{ServersTestSeeds.NoSuchId}", new { name = "Ghost" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>The admin routes are not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().PutAsJsonAsync(
            $"/api/admin/server-groups/{ServersTestSeeds.NoSuchId}", new { name = "Ghost" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
