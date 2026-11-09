using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Clients;

/// <summary><c>DELETE /api/clients/{id}/users/{userId}</c>: unlinking a non-owner user from a client account.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class RemoveUserFromClientTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Removing a linked non-owner user drops the link.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RemoveDropsTheLink()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var (userId, _) = await ClientsTestSeeds.RegisterRealClientAsync(factory, "remove-user-happy");
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        var added = await admin.PostAsJsonAsync($"/api/clients/{clientId}/users", new { memberUserId = userId, permissions = 0 });
        added.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await admin.DeleteAsync($"/api/clients/{clientId}/users/{userId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>Removing a user from an id that names no client answers 404 with <c>CLIENT_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RemoveRefusesUnknownClient()
    {
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.DeleteAsync(
            $"/api/clients/{ClientsTestSeeds.NoSuchClientId}/users/{ClientsTestSeeds.NoSuchUserId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_NOT_FOUND);
    }

    /// <summary>Removing a user is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RemoveRefusesAnonymous()
    {
        var response = await factory.CreateClient().DeleteAsync(
            $"/api/clients/{ClientsTestSeeds.NoSuchClientId}/users/{ClientsTestSeeds.NoSuchUserId}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A client, who is not staff, is refused with 403 on the admin route.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RemoveRefusesAClient()
    {
        var (client, clientId, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "remove-user-forbidden");

        var response = await client.DeleteAsync($"/api/clients/{clientId}/users/{ClientsTestSeeds.NoSuchUserId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>The account owner cannot be removed as a user: the answer is 409 <c>CLIENT_OWNER_CANNOT_BE_REMOVED</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RemoveRefusesTheAccountOwner()
    {
        var (clientId, ownerUserId) = await ClientsTestSeeds.SeedClientAsync(factory);
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.DeleteAsync($"/api/clients/{clientId}/users/{ownerUserId}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_OWNER_CANNOT_BE_REMOVED);
    }

    /// <summary>Removing a user who is not linked answers 404 with <c>CLIENT_USER_NOT_LINKED</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RemoveRefusesAUserWhoIsNotLinked()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.DeleteAsync($"/api/clients/{clientId}/users/{ClientsTestSeeds.NoSuchUserId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_USER_NOT_LINKED);
    }

    /// <summary>Removing a linked user leaves one activity line naming the client and the acting admin.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RemoveIsAuditedWithTheActingAdmin()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var (userId, _) = await ClientsTestSeeds.RegisterRealClientAsync(factory, "remove-user-audit");
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        (await admin.PostAsJsonAsync($"/api/clients/{clientId}/users", new { memberUserId = userId, permissions = 0 }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await admin.DeleteAsync($"/api/clients/{clientId}/users/{userId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        await ClientsTestSeeds.AssertAuditedByAdminAsync(factory, clientId, "Removed User");
    }
}
