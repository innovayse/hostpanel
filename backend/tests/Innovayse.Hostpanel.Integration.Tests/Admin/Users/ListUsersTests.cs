using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Users;

/// <summary>
/// <c>GET /api/admin/users</c>: an admin reads a page of user summaries including one freshly
/// registered, a caller with only the Client role is refused, and the route is closed to a
/// caller with no credential. This use case refuses nothing once authorized.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host, in local mode.</param>
public sealed class ListUsersTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A freshly registered account appears in the admin's page of user summaries.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RegisterThenList_ShowsTheAccountAsync()
    {
        var client = await UsersTestSeeds.AdminClientAsync(factory);
        var (subject, email) = await UsersTestSeeds.RegisterUserAsync(factory.CreateClient());

        var response = await client.GetAsync($"/api/admin/users?search={Uri.EscapeDataString(email)}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("items").EnumerateArray().Should().Contain(i => i.GetProperty("id").GetString() == subject);
    }

    /// <summary>A caller with only the Client role is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToTheClientRoleAsync()
    {
        var client = await UsersTestSeeds.ClientRoleClientAsync(factory, "list-users-forbidden");

        var response = await client.GetAsync("/api/admin/users");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>The admin routes are not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().GetAsync("/api/admin/users");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
