using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Domain.Servers.Enums;
using Innovayse.Hostpanel.Shared.Constants;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Servers;

/// <summary>
/// <c>POST /api/admin/server-groups</c>: creating a group with a server assigns it and the list
/// shows the membership, an empty name is refused <c>VALIDATION_ERROR</c>, a caller with only the
/// Client role is refused, and the route is closed to a caller with no credential. This use case
/// always succeeds once validation has passed.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CreateServerGroupTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Creating a group with a server assigns it, and the list shows the membership.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task WithServers_AssignsThemAsync()
    {
        var client = await ServersTestSeeds.AdminClientAsync(factory);
        var serverId = await ServersTestSeeds.CreateServerAsync(client, $"GroupServer-{Guid.NewGuid():N}");
        var groupName = $"Group-{Guid.NewGuid():N}";

        await ServersTestSeeds.CreateServerGroupAsync(client, groupName, serverId);

        var list = await client.GetFromJsonAsync<List<System.Text.Json.JsonElement>>("/api/admin/server-groups");
        var group = list!.Single(g => g.GetProperty("name").GetString() == groupName);
        group.GetProperty("servers").EnumerateArray().Should().Contain(s => s.GetProperty("id").GetInt32() == serverId);
    }

    /// <summary>A group with an empty name is refused by the validator before it reaches the handler.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnEmptyNameAsync()
    {
        var client = await ServersTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync("/api/admin/server-groups", new
        {
            name = string.Empty,
            fillType = ServerFillType.LeastFull,
            serverIds = Array.Empty<int>(),
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await ServersTestSeeds.AssertCodeAsync(response, SharedErrorCodes.ValidationError);
    }

    /// <summary>A caller with only the Client role is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToTheClientRoleAsync()
    {
        var client = await ServersTestSeeds.ClientRoleClientAsync(factory, "create-server-group-forbidden");

        var response = await client.PostAsJsonAsync("/api/admin/server-groups", new { name = string.Empty });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>The admin routes are not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/admin/server-groups", new { name = string.Empty });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
