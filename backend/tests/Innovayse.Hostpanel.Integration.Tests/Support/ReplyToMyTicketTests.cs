using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Support.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// <c>POST /api/me/tickets/{id}/reply</c>: a client can reply to their own ticket; a stranger
/// replying to another client's ticket is refused <c>TICKET_NOT_FOUND</c>, for the same reason as
/// the read; and the action is closed to a caller with no credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ReplyToMyTicketTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A client can reply to their own ticket.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ClientCanReplyToTheirOwnTicketAsync()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var departmentId = await SupportTestSeeds.CreateDepartmentAsync(admin);
        var (owner, _) = await SupportTestSeeds.RegisterClientAsync(factory, "reply-my-ticket-owner");
        var createResponse = await owner.PostAsJsonAsync("/api/me/tickets", new
        {
            subject = "Help with my account",
            message = "Something is not working.",
            departmentId,
            priority = nameof(TicketPriority.Medium),
        });
        var id = int.Parse((await createResponse.Content.ReadAsStringAsync()).Trim());

        var response = await owner.PostAsJsonAsync(
            $"/api/me/tickets/{id}/reply", new { message = "Any update?" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>A stranger cannot reply to another client's ticket.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ReplyToSomeoneElsesTicketAnswersNotFoundAsync()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var departmentId = await SupportTestSeeds.CreateDepartmentAsync(admin);
        var (_, ownerClientId) = await SupportTestSeeds.RegisterClientAsync(factory, "reply-my-ticket-owner");
        var (stranger, _) = await SupportTestSeeds.RegisterClientAsync(factory, "reply-my-ticket-stranger");

        var createResponse = await admin.PostAsJsonAsync("/api/tickets", new
        {
            clientId = ownerClientId,
            subject = "Owner's own ticket",
            message = "Filed by staff on behalf of the client.",
            departmentId,
            priority = nameof(TicketPriority.Medium),
        });
        var ticketId = int.Parse((await createResponse.Content.ReadAsStringAsync()).Trim());

        var response = await stranger.PostAsJsonAsync(
            $"/api/me/tickets/{ticketId}/reply", new { message = "Let me in" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await SupportTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.TICKET_NOT_FOUND);
    }

    /// <summary>A caller with no credential cannot reply to a client ticket.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            $"/api/me/tickets/{SupportTestSeeds.NoSuchId}/reply", new { message = "Hello" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
