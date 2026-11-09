using System.Net;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Clients;

/// <summary><c>POST /api/clients/{id}/users/{userId}/make-owner</c>: transferring account ownership.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class TransferOwnershipTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// Transferring ownership to a client account makes it the owner, and the previous owner stays on
    /// the account as a user with every permission instead of being locked out of it.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TransferWritesTheNewOwnerAndKeepsThePreviousOne()
    {
        var (clientId, previousOwnerUserId) = await ClientsTestSeeds.SeedClientAsync(factory);
        var (newOwnerUserId, _) = await ClientsTestSeeds.CreateUnlinkedClientSubjectAsync(factory, "transfer-happy");
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsync($"/api/clients/{clientId}/users/{newOwnerUserId}/make-owner", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var client = await db.Clients.AsNoTracking().Include(c => c.Users).SingleAsync(c => c.Id == clientId);
        client.UserId.Should().Be(newOwnerUserId);
        var previous = client.Users.Should().ContainSingle().Subject;
        previous.UserId.Should().Be(previousOwnerUserId);
        previous.Permissions.Should().Be(ClientPermission.All);
    }

    /// <summary>A new owner who is no account at all is refused as unknown, not saved as an owner nobody can sign in as.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TransferRefusesASubjectThatIsNoAccount()
    {
        var (clientId, ownerUserId) = await ClientsTestSeeds.SeedClientAsync(factory);
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsync(
            $"/api/clients/{clientId}/users/{ClientsTestSeeds.NoSuchUserId}/make-owner", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.IDENTITY_USER_NOT_FOUND);
        await AssertOwnerAsync(clientId, ownerUserId);
    }

    /// <summary>A staff account is refused as a new owner: it holds no client role.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TransferRefusesAStaffAccount()
    {
        var (clientId, ownerUserId) = await ClientsTestSeeds.SeedClientAsync(factory);
        var (staffUserId, _) = await ClientsTestSeeds.CreateUnlinkedStaffSubjectAsync(factory, "transfer-staff");
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsync($"/api/clients/{clientId}/users/{staffUserId}/make-owner", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.IDENTITY_USER_NOT_FOUND);
        await AssertOwnerAsync(clientId, ownerUserId);
    }

    /// <summary>A closed account's ownership is frozen: the transfer answers 409 <c>CLIENT_RECORD_CLOSED</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TransferRefusesAClosedClient()
    {
        var (clientId, ownerUserId) = await ClientsTestSeeds.SeedClosedClientAsync(factory);
        var (newOwnerUserId, _) = await ClientsTestSeeds.CreateUnlinkedClientSubjectAsync(factory, "transfer-closed");
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsync($"/api/clients/{clientId}/users/{newOwnerUserId}/make-owner", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_RECORD_CLOSED);
        await AssertOwnerAsync(clientId, ownerUserId);
    }

    /// <summary>A successful transfer leaves one activity line naming the client and the acting admin, and no personal data.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TransferIsAuditedWithTheActingAdmin()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var (newOwnerUserId, _) = await ClientsTestSeeds.CreateUnlinkedClientSubjectAsync(factory, "transfer-audit");
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        (await admin.PostAsync($"/api/clients/{clientId}/users/{newOwnerUserId}/make-owner", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        await ClientsTestSeeds.AssertAuditedByAdminAsync(factory, clientId, "Transferred Ownership");
    }

    /// <summary>Asserts the client is still owned by the given subject.</summary>
    /// <param name="clientId">The client to read.</param>
    /// <param name="ownerUserId">The subject that must still own it.</param>
    /// <returns>A task that completes once the assertion is made.</returns>
    private async Task AssertOwnerAsync(int clientId, string ownerUserId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var client = await db.Clients.AsNoTracking().SingleAsync(c => c.Id == clientId);
        client.UserId.Should().Be(ownerUserId);
    }

    /// <summary>Transferring ownership to a user who already owns another client answers 409, not a database error.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TransferRefusesAUserWhoAlreadyOwnsAnotherClient()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var (existingOwnerUserId, _) = await ClientsTestSeeds.RegisterRealClientAsync(factory, "transfer-dupe");
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsync($"/api/clients/{clientId}/users/{existingOwnerUserId}/make-owner", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.EXISTING_USER_ALREADY_HAS_CLIENT);
    }

    /// <summary>Transferring ownership on an id that names no client answers 404 with <c>CLIENT_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TransferRefusesUnknownClient()
    {
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsync(
            $"/api/clients/{ClientsTestSeeds.NoSuchClientId}/users/{ClientsTestSeeds.NoSuchUserId}/make-owner", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_NOT_FOUND);
    }

    /// <summary>Transferring ownership is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TransferRefusesAnonymous()
    {
        var response = await factory.CreateClient().PostAsync(
            $"/api/clients/{ClientsTestSeeds.NoSuchClientId}/users/{ClientsTestSeeds.NoSuchUserId}/make-owner", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A client, who is not staff, is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TransferRefusesAClient()
    {
        var (client, clientId, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "transfer-forbidden");

        var response = await client.PostAsync(
            $"/api/clients/{clientId}/users/{ClientsTestSeeds.NoSuchUserId}/make-owner", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
