using System.Net;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Servers;

/// <summary>
/// <c>GET /api/admin/servers</c>: an admin reads every configured server, a caller with only the
/// Client role is refused, and the route is closed to a caller with no credential. This use case
/// refuses nothing once authorized.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListServersTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An admin lists the configured servers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminCanListServersAsync()
    {
        var client = await ServersTestSeeds.AdminClientAsync(factory);
        var name = $"Server-{Guid.NewGuid():N}";
        await ServersTestSeeds.CreateServerAsync(client, name);

        var response = await client.GetAsync("/api/admin/servers");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>A caller with only the Client role is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToTheClientRoleAsync()
    {
        var client = await ServersTestSeeds.ClientRoleClientAsync(factory, "list-servers-forbidden");

        var response = await client.GetAsync("/api/admin/servers");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>The admin routes are not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().GetAsync("/api/admin/servers");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
