using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Domain.Support.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// <c>GET /api/me/tickets</c> and <c>POST /api/me/tickets</c> for the users of an account: the owner
/// and a user linked with the tickets permission list the account's tickets and open new ones on
/// it; a user without the permission is refused; a user of another account never sees them.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class LinkedUserTicketListAccessTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The account owner lists the account's ticket.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task OwnerListsTheTicketAsync()
    {
        var (owner, ticketId) = await OwnerWithTicketAsync("linked-list-owner");

        var list = await owner.Client.GetAsync("/api/me/tickets");

        list.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ClientsTestSeeds.IdsOfAsync(list)).Should().Contain(ticketId);
    }

    /// <summary>A user linked with the tickets permission lists the account's ticket and opens a new one on the account.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PermittedLinkedUserListsAndOpensTicketsAsync()
    {
        var (owner, ticketId) = await OwnerWithTicketAsync("linked-list-permitted-owner");
        var user = await ClientsTestSeeds.LinkedToAsync(factory, "linked-list-permitted", owner.ClientId, ClientPermission.ViewOpenSupportTickets);
        var departmentId = await SupportTestSeeds.CreateDepartmentAsync(await SupportTestSeeds.AdminClientAsync(factory));

        var list = await user.GetAsync("/api/me/tickets");
        var opened = await user.PostAsJsonAsync("/api/me/tickets", OpenBody(departmentId));
        var ownerList = await owner.Client.GetAsync("/api/me/tickets");

        list.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ClientsTestSeeds.IdsOfAsync(list)).Should().Contain(ticketId);
        opened.StatusCode.Should().Be(HttpStatusCode.Created);
        (await ClientsTestSeeds.IdsOfAsync(ownerList)).Should().HaveCount(2, "the linked user's ticket is filed on the owner's account");
    }

    /// <summary>A user linked without the tickets permission is refused both routes.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task LinkedUserWithoutThePermissionIsRefusedAsync()
    {
        var (owner, _) = await OwnerWithTicketAsync("linked-list-denied-owner");
        var user = await ClientsTestSeeds.LinkedToAsync(factory, "linked-list-denied", owner.ClientId, ClientPermission.ViewDomains);

        var list = await user.GetAsync("/api/me/tickets");
        var opened = await user.PostAsJsonAsync("/api/me/tickets", OpenBody(1));

        foreach (var response in new[] { list, opened })
        {
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_PERMISSION_DENIED);
        }
    }

    /// <summary>A user permitted on another account lists their own account's tickets only.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PermittedUserOfAnotherAccountDoesNotSeeTheTicketAsync()
    {
        var (_, ticketId) = await OwnerWithTicketAsync("linked-list-foreign-owner");
        var (otherClientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var user = await ClientsTestSeeds.LinkedToAsync(factory, "linked-list-foreign", otherClientId, ClientPermission.ViewOpenSupportTickets);

        var list = await user.GetAsync("/api/me/tickets");

        list.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ClientsTestSeeds.IdsOfAsync(list)).Should().NotContain(ticketId);
    }

    /// <summary>The body <c>POST /api/me/tickets</c> takes.</summary>
    /// <param name="departmentId">The department to open the ticket in.</param>
    /// <returns>The request body.</returns>
    private static object OpenBody(int departmentId) => new
    {
        subject = "Opened by a linked user",
        message = "This ticket belongs to the account.",
        departmentId,
        priority = nameof(TicketPriority.Medium),
    };

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
