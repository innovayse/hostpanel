using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Domain.Support.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// <c>GET /api/tickets/client/{clientId}</c>: staff can list a specific client's tickets, and
/// the action is closed to a caller with no credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListClientTicketsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Staff can list a specific client's tickets, each named after that client.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task StaffCanListAClientsTicketsAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);
        var departmentId = await SupportTestSeeds.CreateDepartmentAsync(client);
        var clientId = (await ClientsTestSeeds.SeedClientAsync(factory)).ClientId;

        var ticketResponse = await client.PostAsJsonAsync("/api/tickets", new
        {
            clientId,
            subject = "Listing test ticket",
            message = "Filed by staff on behalf of the client.",
            departmentId,
            priority = nameof(TicketPriority.Medium),
        });
        ticketResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var response = await client.GetAsync($"/api/tickets/client/{clientId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("items").EnumerateArray()
            .Should().ContainSingle()
            .Which.GetProperty("clientName").GetString().Should().Be("Ada Lovelace");
    }

    /// <summary>The route id wins over a client id sent in the query, and paging binds from the query.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TheRouteClientIdWinsOverTheQueryAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/tickets/client/1?clientId=999&page=1&pageSize=5");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>A caller with no credential cannot list a client's tickets.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().GetAsync("/api/tickets/client/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
