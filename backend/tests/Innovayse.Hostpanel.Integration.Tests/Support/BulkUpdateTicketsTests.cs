using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Support.Common.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// <c>POST /api/tickets/bulk</c>: an admin can apply a bulk action to several tickets at once, an
/// unrecognised action is refused before any ticket is touched, and the action is closed to a
/// caller with no credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class BulkUpdateTicketsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An admin can flag several tickets in one bulk call.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminCanBulkFlagTicketsAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);
        var id = await SupportTestSeeds.CreateAdminTicketAsync(factory, client);

        var response = await client.PostAsJsonAsync(
            "/api/tickets/bulk", new { ticketIds = new[] { id }, action = nameof(BulkTicketAction.Flag) });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>An unrecognised bulk action is refused by the validator before any ticket is touched.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task BulkActionRefusesAnUnknownActionAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);
        var id = await SupportTestSeeds.CreateAdminTicketAsync(factory, client);

        var response = await client.PostAsJsonAsync(
            "/api/tickets/bulk", new { ticketIds = new[] { id }, action = "explode" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>A caller with no credential cannot run a bulk ticket action.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/tickets/bulk", new { ticketIds = new[] { SupportTestSeeds.NoSuchId }, action = nameof(BulkTicketAction.Flag) });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
