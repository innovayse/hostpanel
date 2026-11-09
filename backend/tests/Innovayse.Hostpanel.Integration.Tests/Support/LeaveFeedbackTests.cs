using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// <c>POST /api/tickets/{id}/feedback</c>: staff can record feedback on a ticket, an out-of-range
/// rating is refused by the validator, an id names no ticket is refused <c>TICKET_NOT_FOUND</c>,
/// and the action is closed to a caller with no credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class LeaveFeedbackTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Staff can record feedback for a ticket.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminCanLeaveFeedbackAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);
        var id = await SupportTestSeeds.CreateAdminTicketAsync(factory, client);

        var response = await client.PostAsJsonAsync(
            $"/api/tickets/{id}/feedback", new { rating = 5, comment = "ok", leftBy = "Client" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>
    /// A feedback rating outside 1-10 is refused by the validator, not the domain guard. A
    /// validator refusal is answered 400 VALIDATION_ERROR on the bus (rules/cqrs.md); 422 is
    /// reserved for a handler's own *_VALIDATION_FAILED code.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task LeaveFeedbackRefusesAnOutOfRangeRatingAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);
        var id = await SupportTestSeeds.CreateAdminTicketAsync(factory, client);

        var response = await client.PostAsJsonAsync(
            $"/api/tickets/{id}/feedback", new { rating = 11, comment = "too high", leftBy = "Client" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>Leaving feedback on a ticket that does not exist answers 404 with <c>TICKET_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task LeaveFeedbackOnUnknownTicketAnswersNotFoundAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync(
            $"/api/tickets/{SupportTestSeeds.NoSuchId}/feedback", new { rating = 5, comment = "ok", leftBy = "Client" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await SupportTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.TICKET_NOT_FOUND);
    }

    /// <summary>A caller with no credential cannot leave feedback on a ticket.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            $"/api/tickets/{SupportTestSeeds.NoSuchId}/feedback", new { rating = 5, comment = "ok", leftBy = "Client" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
