using System.Net;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// <c>POST /api/tickets/{id}/toggle-flag</c>: staff can toggle a ticket's flagged state, an id
/// names no ticket is refused <c>TICKET_NOT_FOUND</c>, and the action is closed to a caller with
/// no credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ToggleTicketFlagTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Staff can toggle the flagged state of a ticket.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminCanToggleTheFlagAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);
        var id = await SupportTestSeeds.CreateAdminTicketAsync(factory, client);

        var response = await client.PostAsync($"/api/tickets/{id}/toggle-flag", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>Toggling the flag on a ticket that does not exist answers 404 with <c>TICKET_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ToggleFlagOnUnknownTicketAnswersNotFoundAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsync($"/api/tickets/{SupportTestSeeds.NoSuchId}/toggle-flag", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await SupportTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.TICKET_NOT_FOUND);
    }

    /// <summary>A caller with no credential cannot toggle a ticket's flag.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().PostAsync(
            $"/api/tickets/{SupportTestSeeds.NoSuchId}/toggle-flag", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
