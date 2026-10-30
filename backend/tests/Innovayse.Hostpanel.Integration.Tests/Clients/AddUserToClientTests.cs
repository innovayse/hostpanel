using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Auth.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Clients;

/// <summary><c>POST /api/clients/{id}/users</c>: linking a non-owner user to a client account.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class AddUserToClientTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Linking a real, unlinked subject to a client succeeds.</summary>
    [Fact]
    public async Task AddLinksTheUser()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var (userId, _) = await ClientsTestSeeds.RegisterRealClientAsync(factory, "add-user-happy");
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsJsonAsync($"/api/clients/{clientId}/users", new { memberUserId = userId, permissions = 0 });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>
    /// A linked account that does not hold the Client role, as one staff created elsewhere does not,
    /// is granted it, so the portal serves it the client it was linked to.
    /// </summary>
    [Fact]
    public async Task AddGrantsTheClientRole()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var (userId, _) = await ClientsTestSeeds.RegisterRealClientAsync(factory, "add-user-role");
        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISubjectRoleStore>().RemoveAsync(userId, Role.Client, CancellationToken.None);
        }

        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsJsonAsync($"/api/clients/{clientId}/users", new { memberUserId = userId, permissions = 0 });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var check = factory.Services.CreateScope();
        (await check.ServiceProvider.GetRequiredService<ISubjectRoleStore>().HoldsAsync(userId, Role.Client, CancellationToken.None))
            .Should().BeTrue();
    }

    /// <summary>Linking a user to an id that names no client answers 404 with <c>CLIENT_NOT_FOUND</c>.</summary>
    [Fact]
    public async Task AddRefusesUnknownClient()
    {
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsJsonAsync(
            $"/api/clients/{ClientsTestSeeds.NoSuchClientId}/users", new { memberUserId = ClientsTestSeeds.NoSuchUserId, permissions = 0 });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_NOT_FOUND);
    }

    /// <summary>Linking an unknown Identity subject to a client answers <c>IDENTITY_USER_NOT_FOUND</c>.</summary>
    [Fact]
    public async Task AddRefusesAnUnknownIdentity()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsJsonAsync(
            $"/api/clients/{clientId}/users", new { memberUserId = ClientsTestSeeds.NoSuchUserId, permissions = 0 });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.IDENTITY_USER_NOT_FOUND);
    }

    /// <summary>Linking a user to a client is not for anonymous callers.</summary>
    [Fact]
    public async Task AddRefusesAnonymous()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            $"/api/clients/{ClientsTestSeeds.NoSuchClientId}/users", new { memberUserId = ClientsTestSeeds.NoSuchUserId, permissions = 0 });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A client, who is not staff, is refused with 403 on the admin route.</summary>
    [Fact]
    public async Task AddRefusesAClient()
    {
        var (client, clientId, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "add-user-forbidden");

        var response = await client.PostAsJsonAsync(
            $"/api/clients/{clientId}/users", new { memberUserId = ClientsTestSeeds.NoSuchUserId, permissions = 0 });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>A staff account is never linked to a client: it is refused as unknown and no link is written.</summary>
    [Fact]
    public async Task AddRefusesAStaffAccount()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var (staffUserId, _) = await ClientsTestSeeds.CreateUnlinkedStaffSubjectAsync(factory, "add-user-staff");
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsJsonAsync(
            $"/api/clients/{clientId}/users", new { memberUserId = staffUserId, permissions = 0 });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.IDENTITY_USER_NOT_FOUND);
    }

    /// <summary>A closed account's users are frozen: linking one answers 409 <c>CLIENT_RECORD_CLOSED</c>.</summary>
    [Fact]
    public async Task AddRefusesAClosedClient()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClosedClientAsync(factory);
        var (userId, _) = await ClientsTestSeeds.RegisterRealClientAsync(factory, "add-user-closed");
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsJsonAsync(
            $"/api/clients/{clientId}/users", new { memberUserId = userId, permissions = 0 });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_RECORD_CLOSED);
    }

    /// <summary>Linking a user leaves one activity line naming the client and the acting admin.</summary>
    [Fact]
    public async Task AddIsAuditedWithTheActingAdmin()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var (userId, _) = await ClientsTestSeeds.RegisterRealClientAsync(factory, "add-user-audit");
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        (await admin.PostAsJsonAsync($"/api/clients/{clientId}/users", new { memberUserId = userId, permissions = 0 }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        await ClientsTestSeeds.AssertAuditedByAdminAsync(factory, clientId, "Added User");
    }
}
