using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// <c>DELETE /api/tickets/{id}/tags/{tag}</c>: staff can remove a tag from a ticket, an id names
/// no ticket is refused <c>TICKET_NOT_FOUND</c>, and the action is closed to a caller with no
/// credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class RemoveTicketTagTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Staff can remove a tag that was previously added to a ticket.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminCanRemoveATagAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);
        var id = await SupportTestSeeds.CreateAdminTicketAsync(factory, client);
        await client.PostAsJsonAsync($"/api/tickets/{id}/tags", new { tag = "urgent" });

        var response = await client.DeleteAsync($"/api/tickets/{id}/tags/urgent");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>Removing a tag from a ticket that does not exist answers 404 with <c>TICKET_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RemoveTagFromUnknownTicketAnswersNotFoundAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);

        var response = await client.DeleteAsync($"/api/tickets/{SupportTestSeeds.NoSuchId}/tags/urgent");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await SupportTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.TICKET_NOT_FOUND);
    }

    /// <summary>A caller with no credential cannot remove a tag from a ticket.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().DeleteAsync($"/api/tickets/{SupportTestSeeds.NoSuchId}/tags/urgent");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
