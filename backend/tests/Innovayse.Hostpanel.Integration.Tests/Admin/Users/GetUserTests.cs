using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Users;

/// <summary>
/// <c>GET /api/admin/users/{id}</c>: a registered account can be read back by subject, an unknown
/// subject is refused <c>IDENTITY_USER_NOT_FOUND</c>, a caller with only the Client role is
/// refused, and the route is closed to a caller with no credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host, in local mode.</param>
public sealed class GetUserTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A registered account can be read back by id.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RegisterThenGetById_ReturnsTheAccountAsync()
    {
        var client = await UsersTestSeeds.AdminClientAsync(factory);
        var (subject, email) = await UsersTestSeeds.RegisterUserAsync(factory.CreateClient());

        var response = await client.GetAsync($"/api/admin/users/{subject}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("email").GetString().Should().Be(email);
    }

    /// <summary>Reading an unknown subject answers 404 with <c>IDENTITY_USER_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UnknownSubject_RefusesNotFoundAsync()
    {
        var client = await UsersTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync($"/api/admin/users/{UsersTestSeeds.NoSuchSubject}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await UsersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.IDENTITY_USER_NOT_FOUND);
    }

    /// <summary>A caller with only the Client role is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToTheClientRoleAsync()
    {
        var client = await UsersTestSeeds.ClientRoleClientAsync(factory, "get-user-forbidden");

        var response = await client.GetAsync($"/api/admin/users/{UsersTestSeeds.NoSuchSubject}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>The admin routes are not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().GetAsync($"/api/admin/users/{UsersTestSeeds.NoSuchSubject}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
