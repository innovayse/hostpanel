using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Support.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// <c>/api/me/tickets</c> for a member an identity plugin vouches for: they list, read, reply to and open
/// tickets on the client linked to the plugin account and never on the client record they registered with.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class PluginMemberTicketAccessTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The member lists, reads and replies to the linked client's ticket, and the member's own client's ticket stays out of reach.</summary>
    [Fact]
    public async Task MemberActsForThePluginAccountNotForTheirOwnClientRecordAsync()
    {
        var (member, ticketId, admin) = await MemberWithTicketAsync("plugin-ticket-scope");
        using var memberLifetime = member;
        var ownTicketId = await SupportTestSeeds.CreateAdminTicketAsync(factory, admin, member.OwnClientId);

        var list = await member.Client.GetAsync("/api/me/tickets");
        var read = await member.Client.GetAsync($"/api/me/tickets/{ticketId}");
        var reply = await member.Client.PostAsJsonAsync($"/api/me/tickets/{ticketId}/reply", new { message = "From a plugin member." });
        var ownRead = await member.Client.GetAsync($"/api/me/tickets/{ownTicketId}");
        var ownReply = await member.Client.PostAsJsonAsync($"/api/me/tickets/{ownTicketId}/reply", new { message = "Not this account." });

        list.StatusCode.Should().Be(HttpStatusCode.OK);
        var ids = await ClientsTestSeeds.IdsOfAsync(list);
        ids.Should().Contain(ticketId);
        ids.Should().NotContain(ownTicketId);
        read.StatusCode.Should().Be(HttpStatusCode.OK);
        reply.StatusCode.Should().Be(HttpStatusCode.NoContent);
        foreach (var response in new[] { ownRead, ownReply })
        {
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
            await SupportTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.TICKET_NOT_FOUND);
        }
    }

    /// <summary>A ticket the member opens is filed on the linked client, not on the member's own record.</summary>
    [Fact]
    public async Task TicketTheMemberOpens_IsFiledOnTheLinkedClientAsync()
    {
        var (member, _, admin) = await MemberWithTicketAsync("plugin-ticket-open");
        using var memberLifetime = member;
        var departmentId = await SupportTestSeeds.CreateDepartmentAsync(admin);

        var opened = await member.Client.PostAsJsonAsync("/api/me/tickets", new
        {
            subject = "Opened by a plugin member",
            message = "Filed on the plugin account.",
            departmentId,
            priority = nameof(TicketPriority.Medium),
        });
        var list = await member.Client.GetAsync("/api/me/tickets");

        opened.StatusCode.Should().Be(HttpStatusCode.Created);
        var openedId = int.Parse((await opened.Content.ReadAsStringAsync()).Trim());
        (await ClientsTestSeeds.IdsOfAsync(list)).Should().Contain(openedId);
    }

    /// <summary>A member of an account no client is linked to yet gets no profile rather than another client's tickets.</summary>
    [Fact]
    public async Task MemberOfAnAccountWithNoClient_GetsNoProfileAsync()
    {
        using var member = await PluginMemberSeeds.SignInMemberAsync(factory, "plugin-ticket-unlinked", $"acct-{Guid.NewGuid():N}");

        var list = await member.Client.GetAsync("/api/me/tickets");

        list.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(list, HostpanelErrorCodes.CLIENT_PROFILE_NOT_FOUND);
    }

    /// <summary>Seeds a client with a ticket filed by staff, linked to a plugin account, and a member of that account.</summary>
    /// <param name="label">Distinguishes the registered address between tests.</param>
    /// <returns>The member, the ticket id on the linked client and the admin caller that seeded it.</returns>
    private async Task<(PluginMember Member, int TicketId, HttpClient Admin)> MemberWithTicketAsync(string label)
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var seeded = await ClientsTestSeeds.SeedClientAsync(factory);
        var ticketId = await SupportTestSeeds.CreateAdminTicketAsync(factory, admin, seeded.ClientId);
        var accountId = await PluginMemberSeeds.LinkAccountAsync(factory, seeded.ClientId);
        var member = await PluginMemberSeeds.SignInMemberAsync(factory, label, accountId);

        return (member, ticketId, admin);
    }
}
