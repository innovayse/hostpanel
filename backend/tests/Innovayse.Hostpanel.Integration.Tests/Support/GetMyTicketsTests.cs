using System.Net;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// <c>GET /api/me/tickets</c>: a client can list their own tickets, and the action is closed to a
/// caller with no credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetMyTicketsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A registered client can list their own tickets.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ClientCanListTheirOwnTicketsAsync()
    {
        var (client, _) = await SupportTestSeeds.RegisterClientAsync(factory, "get-my-tickets-owner");

        var response = await client.GetAsync("/api/me/tickets");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>A caller with no credential cannot list client tickets.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().GetAsync("/api/me/tickets");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
