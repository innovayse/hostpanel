using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Clients;

/// <summary><c>POST /api/clients/{id}/users/invite</c>: the admin-side invitation of a user to a client account.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class InviteUserToClientTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Inviting a fresh address to a real client succeeds.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task InviteSendsTheInvitation()
    {
        var (_, ownerEmail) = await ClientsTestSeeds.RegisterRealClientAsync(factory, "invite-owner");
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        var clientId = await FindClientIdAsync(admin, ownerEmail);

        var response = await admin.PostAsJsonAsync($"/api/clients/{clientId}/users/invite", new
        {
            email = $"invitee-{Guid.NewGuid():N}@example.test",
            firstName = "Invitee",
            lastName = "Person",
            permissions = 1,
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>An invitation leaves one activity line for the client, stamped with the acting admin and free of the invited address.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task InviteWritesAnAuditLine()
    {
        var (_, ownerEmail) = await ClientsTestSeeds.RegisterRealClientAsync(factory, "invite-audit");
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        var clientId = await FindClientIdAsync(admin, ownerEmail);

        var response = await admin.PostAsJsonAsync($"/api/clients/{clientId}/users/invite", new
        {
            email = $"invitee-{Guid.NewGuid():N}@example.test",
            firstName = "Invitee",
            lastName = "Person",
            permissions = 1,
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await ClientsTestSeeds.AssertAuditedByAdminAsync(factory, clientId, "Invited User");
    }

    /// <summary>Inviting to an id that names no client answers 404 with <c>CLIENT_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task InviteRefusesUnknownClient()
    {
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsJsonAsync($"/api/clients/{ClientsTestSeeds.NoSuchClientId}/users/invite", new
        {
            email = "invitee@example.test",
            firstName = "Invitee",
            lastName = "Person",
            permissions = 1,
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_NOT_FOUND);
    }

    /// <summary>Inviting the account owner's own address answers <c>CANNOT_INVITE_ACCOUNT_OWNER</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task InviteRefusesTheOwnersOwnAddress()
    {
        var (_, ownerEmail) = await ClientsTestSeeds.RegisterRealClientAsync(factory, "invite-self");
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        var clientId = await FindClientIdAsync(admin, ownerEmail);

        var response = await admin.PostAsJsonAsync($"/api/clients/{clientId}/users/invite", new
        {
            email = ownerEmail,
            firstName = "Invitee",
            lastName = "Person",
            permissions = 1,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CANNOT_INVITE_ACCOUNT_OWNER);
    }

    /// <summary>Inviting the same address twice answers <c>INVITATION_ALREADY_PENDING</c> the second time.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task InviteRefusesADuplicatePendingInvitation()
    {
        var (_, ownerEmail) = await ClientsTestSeeds.RegisterRealClientAsync(factory, "invite-dupe");
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        var clientId = await FindClientIdAsync(admin, ownerEmail);
        var inviteeEmail = $"invitee-{Guid.NewGuid():N}@example.test";
        var first = await admin.PostAsJsonAsync($"/api/clients/{clientId}/users/invite", new
        {
            email = inviteeEmail,
            firstName = "Invitee",
            lastName = "Person",
            permissions = 1,
        });
        first.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await admin.PostAsJsonAsync($"/api/clients/{clientId}/users/invite", new
        {
            email = inviteeEmail,
            firstName = "Invitee",
            lastName = "Person",
            permissions = 1,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.INVITATION_ALREADY_PENDING);
    }

    /// <summary>Inviting onto a client whose owner subject resolves to nobody answers <c>ACCOUNT_OWNER_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task InviteRefusesWhenTheOwnerCannotBeResolved()
    {
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory, firstName: "Orphan", lastName: "Owner");
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsJsonAsync($"/api/clients/{clientId}/users/invite", new
        {
            email = $"invitee-{Guid.NewGuid():N}@example.test",
            firstName = "Invitee",
            lastName = "Person",
            permissions = 1,
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.ACCOUNT_OWNER_NOT_FOUND);
    }

    /// <summary>Inviting an address already linked to the client answers <c>USER_ALREADY_LINKED</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task InviteRefusesAnAlreadyLinkedAddress()
    {
        var (_, ownerEmail) = await ClientsTestSeeds.RegisterRealClientAsync(factory, "invite-linked-owner");
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        var clientId = await FindClientIdAsync(admin, ownerEmail);
        var (linkedUserId, linkedEmail) = await ClientsTestSeeds.RegisterRealClientAsync(factory, "invite-linked-member");
        var linked = await admin.PostAsJsonAsync($"/api/clients/{clientId}/users", new { memberUserId = linkedUserId, permissions = 0 });
        linked.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await admin.PostAsJsonAsync($"/api/clients/{clientId}/users/invite", new
        {
            email = linkedEmail,
            firstName = "Invitee",
            lastName = "Person",
            permissions = 1,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.USER_ALREADY_LINKED);
    }

    /// <summary>Inviting a user is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task InviteRefusesAnonymous()
    {
        var response = await factory.CreateClient().PostAsJsonAsync($"/api/clients/{ClientsTestSeeds.NoSuchClientId}/users/invite", new
        {
            email = "invitee@example.test",
            firstName = "Invitee",
            lastName = "Person",
            permissions = 1,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A client, who is not staff, is refused with 403 on the admin route.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task InviteRefusesAClient()
    {
        var (client, clientId, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "invite-forbidden");

        var response = await client.PostAsJsonAsync($"/api/clients/{clientId}/users/invite", new
        {
            email = "invitee@example.test",
            firstName = "Invitee",
            lastName = "Person",
            permissions = 1,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>Finds the client row the admin panel assigned to a freshly registered email.</summary>
    /// <param name="admin">The authenticated admin client.</param>
    /// <param name="email">The registered account's email.</param>
    /// <returns>The matching client's primary key.</returns>
    private static async Task<int> FindClientIdAsync(HttpClient admin, string email)
    {
        var list = await admin.GetFromJsonAsync<System.Text.Json.JsonElement>(
            $"/api/clients?page=1&pageSize=100&email={Uri.EscapeDataString(email)}");
        return list.GetProperty("items").EnumerateArray().Single().GetProperty("id").GetInt32();
    }
}
