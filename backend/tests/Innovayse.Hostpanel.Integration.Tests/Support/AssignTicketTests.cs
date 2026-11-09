using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// <c>POST /api/tickets/{id}/assign</c>: staff can assign a ticket to a staff member, an id
/// names no ticket is refused <c>TICKET_NOT_FOUND</c>, and the action is closed to a caller with
/// no credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class AssignTicketTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Staff can assign a ticket to a staff member.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminCanAssignATicketAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);
        var id = await SupportTestSeeds.CreateAdminTicketAsync(factory, client);

        var response = await client.PostAsJsonAsync($"/api/tickets/{id}/assign", new { staffId = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>Assigning a ticket that does not exist answers 404 with <c>TICKET_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AssignUnknownTicketAnswersNotFoundAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync($"/api/tickets/{SupportTestSeeds.NoSuchId}/assign", new { staffId = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await SupportTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.TICKET_NOT_FOUND);
    }

    /// <summary>A caller with no credential cannot assign a ticket.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            $"/api/tickets/{SupportTestSeeds.NoSuchId}/assign", new { staffId = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
