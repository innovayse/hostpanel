using System.Net;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Servers;

/// <summary>
/// <c>GET /api/admin/server-groups</c>: an admin reads every configured group with its assigned
/// servers, a caller with only the Client role is refused, and the route is closed to a caller
/// with no credential. This use case refuses nothing once authorized.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListServerGroupsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An admin lists the configured server groups.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminCanListGroupsAsync()
    {
        var client = await ServersTestSeeds.AdminClientAsync(factory);
        await ServersTestSeeds.CreateServerGroupAsync(client, $"Group-{Guid.NewGuid():N}");

        var response = await client.GetAsync("/api/admin/server-groups");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>A caller with only the Client role is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToTheClientRoleAsync()
    {
        var client = await ServersTestSeeds.ClientRoleClientAsync(factory, "list-server-groups-forbidden");

        var response = await client.GetAsync("/api/admin/server-groups");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>The admin routes are not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().GetAsync("/api/admin/server-groups");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
