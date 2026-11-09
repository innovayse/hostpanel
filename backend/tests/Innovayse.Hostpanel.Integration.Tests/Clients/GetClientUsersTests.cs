using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Clients;

/// <summary><c>GET /api/clients/{id}/users</c>: the owner and any additional users linked to a client.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetClientUsersTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A real client's users lists its owner.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListIncludesTheOwner()
    {
        var (userId, email) = await ClientsTestSeeds.RegisterRealClientAsync(factory, "get-users-owner");
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        var clientId = await FindClientIdAsync(admin, email);

        var response = await admin.GetAsync($"/api/clients/{clientId}/users");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.EnumerateArray().Select(u => u.GetProperty("userId").GetString()).Should().Contain(userId);
    }

    /// <summary>Listing users for an id that names no client answers 404 with <c>CLIENT_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListRefusesUnknownClient()
    {
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/clients/{ClientsTestSeeds.NoSuchClientId}/users");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_NOT_FOUND);
    }

    /// <summary>Listing a client's users is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListRefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync($"/api/clients/{ClientsTestSeeds.NoSuchClientId}/users");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A client, who is not staff, is refused with 403 on the admin route.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListRefusesAClient()
    {
        var (client, clientId, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "get-users-forbidden");

        var response = await client.GetAsync($"/api/clients/{clientId}/users");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>Finds the client row the admin panel assigned to a freshly registered email.</summary>
    /// <param name="admin">The authenticated admin client.</param>
    /// <param name="email">The registered account's email.</param>
    /// <returns>The matching client's primary key.</returns>
    private static async Task<int> FindClientIdAsync(HttpClient admin, string email)
    {
        var list = await admin.GetFromJsonAsync<JsonElement>($"/api/clients?page=1&pageSize=100&email={Uri.EscapeDataString(email)}");
        return list.GetProperty("items").EnumerateArray().Single().GetProperty("id").GetInt32();
    }
}
