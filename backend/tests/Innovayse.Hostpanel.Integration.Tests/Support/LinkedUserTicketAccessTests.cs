using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Clients.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// <c>GET /api/me/tickets/{id}</c> and <c>POST /api/me/tickets/{id}/reply</c> for the users of an
/// account: the owner and a user linked with the tickets permission read and reply to the
/// account's tickets; a user without the permission, or permitted only on another account, does not.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class LinkedUserTicketAccessTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The account owner reads and replies to a ticket filed on the account.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task OwnerReadsAndRepliesAsync()
    {
        var (owner, ticketId) = await OwnerWithTicketAsync("linked-ticket-owner");

        var read = await owner.Client.GetAsync($"/api/me/tickets/{ticketId}");
        var reply = await owner.Client.PostAsJsonAsync($"/api/me/tickets/{ticketId}/reply", new { message = "From the owner." });

        read.StatusCode.Should().Be(HttpStatusCode.OK);
        reply.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>A user linked to the account with the tickets permission reads and replies to its ticket.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PermittedLinkedUserReadsAndRepliesAsync()
    {
        var (owner, ticketId) = await OwnerWithTicketAsync("linked-ticket-permitted-owner");
        var user = await ClientsTestSeeds.LinkedToAsync(factory, "linked-ticket-permitted", owner.ClientId, ClientPermission.ViewOpenSupportTickets);

        var read = await user.GetAsync($"/api/me/tickets/{ticketId}");
        var reply = await user.PostAsJsonAsync($"/api/me/tickets/{ticketId}/reply", new { message = "From a linked user." });

        read.StatusCode.Should().Be(HttpStatusCode.OK);
        reply.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>
    /// A user linked to the account without the tickets permission is refused both actions, and the
    /// reply never lands on the ticket.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task LinkedUserWithoutThePermissionIsRefusedAsync()
    {
        var (owner, ticketId) = await OwnerWithTicketAsync("linked-ticket-denied-owner");
        var user = await ClientsTestSeeds.LinkedToAsync(factory, "linked-ticket-denied", owner.ClientId, ClientPermission.ViewDomains);

        var read = await user.GetAsync($"/api/me/tickets/{ticketId}");
        var reply = await user.PostAsJsonAsync($"/api/me/tickets/{ticketId}/reply", new { message = "Should not land." });

        foreach (var response in new[] { read, reply })
        {
            response.IsSuccessStatusCode.Should().BeFalse();
        }

        var ticket = await owner.Client.GetStringAsync($"/api/me/tickets/{ticketId}");
        ticket.Should().NotContain("Should not land.");
    }

    /// <summary>
    /// A user permitted on one account is answered <c>TICKET_NOT_FOUND</c> for a ticket of another
    /// account, exactly like an id naming no ticket.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PermittedUserOfAnotherAccountAnswersNotFoundAsync()
    {
        var (_, ticketId) = await OwnerWithTicketAsync("linked-ticket-foreign-owner");
        var (otherClientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var user = await ClientsTestSeeds.LinkedToAsync(factory, "linked-ticket-foreign", otherClientId, ClientPermission.ViewOpenSupportTickets);

        var read = await user.GetAsync($"/api/me/tickets/{ticketId}");
        var reply = await user.PostAsJsonAsync($"/api/me/tickets/{ticketId}/reply", new { message = "Not my account." });

        foreach (var response in new[] { read, reply })
        {
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
            await SupportTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.TICKET_NOT_FOUND);
        }
    }

    /// <summary>Registers an account owner and files a ticket on the account as staff.</summary>
    /// <param name="label">Distinguishes the registered address between tests.</param>
    /// <returns>The signed-in owner and the ticket id.</returns>
    private async Task<(SignedInClient Owner, int TicketId)> OwnerWithTicketAsync(string label)
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var owner = await SupportTestSeeds.RegisterClientAsync(factory, label);
        var ticketId = await SupportTestSeeds.CreateAdminTicketAsync(factory, admin, owner.ClientId);
        return (owner, ticketId);
    }
}
