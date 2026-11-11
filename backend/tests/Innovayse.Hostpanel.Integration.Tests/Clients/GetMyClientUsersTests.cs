using System.Net;
using System.Text.Json;

namespace Innovayse.Hostpanel.Integration.Tests.Clients;

/// <summary>
/// Integration tests for the <c>GetMyClientUsers</c> use case: <c>GET /api/me/users</c> resolves the
/// caller inside the handler and lists the people linked to their own account.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetMyClientUsersTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The list includes the caller as the account owner.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListIncludesTheCaller()
    {
        var (client, _, userId) = await ClientsTestSeeds.RegisterClientAsync(factory, "my-users-list");

        var response = await client.GetAsync("/api/me/users");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.EnumerateArray().Select(u => u.GetProperty("userId").GetString()).Should().Contain(userId);
    }

    /// <summary>The list is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListRefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/me/users");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
