using System.Net;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// <c>POST /api/tickets/{id}/close</c>: staff can close a ticket, an id names no ticket is
/// refused <c>TICKET_NOT_FOUND</c>, and the action is closed to a caller with no credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CloseTicketTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Staff can close a ticket.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminCanCloseATicketAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);
        var id = await SupportTestSeeds.CreateAdminTicketAsync(factory, client);

        var response = await client.PostAsync($"/api/tickets/{id}/close", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>Closing a ticket that does not exist answers 404 with <c>TICKET_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CloseUnknownTicketAnswersNotFoundAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsync($"/api/tickets/{SupportTestSeeds.NoSuchId}/close", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await SupportTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.TICKET_NOT_FOUND);
    }

    /// <summary>A caller with no credential cannot close a ticket.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().PostAsync($"/api/tickets/{SupportTestSeeds.NoSuchId}/close", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
