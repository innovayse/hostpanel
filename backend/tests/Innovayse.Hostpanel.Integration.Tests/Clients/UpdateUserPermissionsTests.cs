using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Clients;

/// <summary><c>PUT /api/clients/{id}/users/{userId}/permissions</c>: changing a linked user's permissions.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpdateUserPermissionsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Updating permissions for a linked user succeeds.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateWritesThePermissions()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var (userId, _) = await ClientsTestSeeds.RegisterRealClientAsync(factory, "update-perm-happy");
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        var added = await admin.PostAsJsonAsync($"/api/clients/{clientId}/users", new { memberUserId = userId, permissions = 0 });
        added.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await admin.PutAsJsonAsync($"/api/clients/{clientId}/users/{userId}/permissions", new { permissions = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>Updating permissions on an id that names no client answers 404 with <c>CLIENT_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateRefusesUnknownClient()
    {
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.PutAsJsonAsync(
            $"/api/clients/{ClientsTestSeeds.NoSuchClientId}/users/{ClientsTestSeeds.NoSuchUserId}/permissions",
            new { permissions = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_NOT_FOUND);
    }

    /// <summary>Updating permissions is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateRefusesAnonymous()
    {
        var response = await factory.CreateClient().PutAsJsonAsync(
            $"/api/clients/{ClientsTestSeeds.NoSuchClientId}/users/{ClientsTestSeeds.NoSuchUserId}/permissions",
            new { permissions = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A client, who is not staff, is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateRefusesAClient()
    {
        var (client, clientId, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "update-perm-forbidden");

        var response = await client.PutAsJsonAsync(
            $"/api/clients/{clientId}/users/{ClientsTestSeeds.NoSuchUserId}/permissions", new { permissions = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>Changing a user's permissions leaves one activity line naming the client and the acting admin.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateIsAuditedWithTheActingAdmin()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var (userId, _) = await ClientsTestSeeds.RegisterRealClientAsync(factory, "update-perm-audit");
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        (await admin.PostAsJsonAsync($"/api/clients/{clientId}/users", new { memberUserId = userId, permissions = 0 }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await admin.PutAsJsonAsync($"/api/clients/{clientId}/users/{userId}/permissions", new { permissions = 1 }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        await ClientsTestSeeds.AssertAuditedByAdminAsync(factory, clientId, "Modified User Permissions");
    }
}
