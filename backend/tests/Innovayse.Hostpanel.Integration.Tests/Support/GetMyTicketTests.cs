using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Support.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// <c>GET /api/me/tickets/{id}</c>: a client can read their own ticket; a stranger's ticket and an
/// id naming no ticket at all both answer 404 with <c>TICKET_NOT_FOUND</c> (ids here are
/// sequential, and telling those two apart is itself a way of enumerating them) and the action
/// is closed to a caller with no credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetMyTicketTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A client can read back their own ticket.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ClientCanReadTheirOwnTicketAsync()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var departmentId = await SupportTestSeeds.CreateDepartmentAsync(admin);
        var (owner, _) = await SupportTestSeeds.RegisterClientAsync(factory, "get-my-ticket-owner");
        var createResponse = await owner.PostAsJsonAsync("/api/me/tickets", new
        {
            subject = "Help with my account",
            message = "Something is not working.",
            departmentId,
            priority = nameof(TicketPriority.Medium),
        });
        var id = int.Parse((await createResponse.Content.ReadAsStringAsync()).Trim());

        var response = await owner.GetAsync($"/api/me/tickets/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// The client's own ticket, read alone or in the list, carries none of the staff-only fields:
    /// a staff tag and the staff flag set on it stay out.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task OwnTicketCarriesNoStaffTagFlagOrAssignmentAsync()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var departmentId = await SupportTestSeeds.CreateDepartmentAsync(admin);
        var (owner, _) = await SupportTestSeeds.RegisterClientAsync(factory, "get-my-ticket-internals");
        var createResponse = await owner.PostAsJsonAsync("/api/me/tickets", new
        {
            subject = "Help with my account",
            message = "Something is not working.",
            departmentId,
            priority = nameof(TicketPriority.Medium),
        });
        var id = int.Parse((await createResponse.Content.ReadAsStringAsync()).Trim());
        var tag = $"staff-tag-{Guid.NewGuid():N}";
        (await admin.PostAsJsonAsync($"/api/tickets/{id}/tags", new { tag })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await admin.PostAsync($"/api/tickets/{id}/toggle-flag", content: null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var single = await owner.GetStringAsync($"/api/me/tickets/{id}");
        var list = await owner.GetStringAsync("/api/me/tickets");

        foreach (var json in new[] { single, list })
        {
            json.Should().NotContain(tag);
            foreach (var field in new[] { "tags", "isFlagged", "assignedToStaffId", "assignedToStaffName", "feedbackLeftBy", "clientId", "clientName" })
            {
                json.Should().NotContain($"\"{field}\"");
            }
        }
    }

    /// <summary>
    /// A stranger reading another client's ticket, and an id naming no ticket at all, are both
    /// refused with <c>TICKET_NOT_FOUND</c>.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetSomeoneElsesTicketAnswersNotFoundAsync()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var departmentId = await SupportTestSeeds.CreateDepartmentAsync(admin);
        var (owner, ownerClientId) = await SupportTestSeeds.RegisterClientAsync(factory, "get-my-ticket-owner");
        var (stranger, _) = await SupportTestSeeds.RegisterClientAsync(factory, "get-my-ticket-stranger");

        var createResponse = await admin.PostAsJsonAsync("/api/tickets", new
        {
            clientId = ownerClientId,
            subject = "Owner's own ticket",
            message = "Filed by staff on behalf of the client.",
            departmentId,
            priority = nameof(TicketPriority.Medium),
        });
        var ticketId = int.Parse((await createResponse.Content.ReadAsStringAsync()).Trim());

        var strangerResponse = await stranger.GetAsync($"/api/me/tickets/{ticketId}");
        var missingResponse = await owner.GetAsync($"/api/me/tickets/{SupportTestSeeds.NoSuchId}");

        strangerResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        missingResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await SupportTestSeeds.AssertCodeAsync(strangerResponse, HostpanelErrorCodes.TICKET_NOT_FOUND);
        await SupportTestSeeds.AssertCodeAsync(missingResponse, HostpanelErrorCodes.TICKET_NOT_FOUND);
    }

    /// <summary>A caller with no credential cannot read a client ticket.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().GetAsync($"/api/me/tickets/{SupportTestSeeds.NoSuchId}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
