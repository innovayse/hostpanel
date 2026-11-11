using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Clients;

/// <summary>
/// Integration tests for the <c>RemoveMyUser</c> use case: <c>DELETE /api/me/users/{userId}</c>
/// resolves the caller inside the handler and removes someone from their own account.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class RemoveMyUserTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A linked second user is removed from the caller's own account and no longer listed.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RemoveDropsTheLinkedUserFromTheCallersAccount()
    {
        var (client, clientId, ownerId) = await ClientsTestSeeds.RegisterClientAsync(factory, "my-users-remove");
        var (memberId, _) = await ClientsTestSeeds.RegisterRealClientAsync(factory, "my-users-remove-member");
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        (await admin.PostAsJsonAsync($"/api/clients/{clientId}/users", new { memberUserId = memberId, permissions = 0 }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ListedUserIdsAsync(client)).Should().Contain(memberId);

        var response = await client.DeleteAsync($"/api/me/users/{memberId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var after = await ListedUserIdsAsync(client);
        after.Should().NotContain(memberId);
        after.Should().Contain(ownerId);
    }

    /// <summary>A user id that is not linked to the caller's account answers 404 with <c>CLIENT_USER_NOT_LINKED</c>.</summary>
    /// <remarks>An id that is not on the caller's account reads as absent, so <c>StatusMapping</c> answers 404.</remarks>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RemoveRefusesAnUnknownUser()
    {
        var (client, _, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "my-users-remove-unknown");

        var response = await client.DeleteAsync($"/api/me/users/user-{Guid.NewGuid():N}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_USER_NOT_LINKED);
    }

    /// <summary>Removing a user is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RemoveRefusesAnonymous()
    {
        var response = await factory.CreateClient().DeleteAsync("/api/me/users/someone");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Reads the user ids <c>GET /api/me/users</c> lists for the caller's account.</summary>
    /// <param name="client">The signed-in client.</param>
    /// <returns>The listed user ids.</returns>
    private static async Task<List<string?>> ListedUserIdsAsync(HttpClient client)
    {
        var listed = await client.GetFromJsonAsync<JsonElement>("/api/me/users");
        return listed.EnumerateArray().Select(u => u.GetProperty("userId").GetString()).ToList();
    }
}
