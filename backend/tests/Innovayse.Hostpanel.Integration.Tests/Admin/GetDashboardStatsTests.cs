using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Support;
using Innovayse.Hostpanel.Domain.Support.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Admin;

/// <summary>
/// <c>GET /api/admin/dashboard/stats</c>: an admin reads every counter, including the
/// open-ticket count which reflects only <see cref="TicketStatus.Open"/> tickets, a caller with
/// only the Client role is refused, and the route is closed to a caller with no credential. This
/// use case refuses nothing once authorized.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetDashboardStatsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Dashboard stats answer 200 with every counter present and no revenue figure, which the charts own.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ReturnsEveryCounterAsync()
    {
        var client = await AdminTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/admin/dashboard/stats");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.TryGetProperty("activeServices", out _).Should().BeTrue();
        body.TryGetProperty("overdueInvoices", out _).Should().BeTrue();
        body.TryGetProperty("openTickets", out _).Should().BeTrue();
        body.TryGetProperty("totalClients", out _).Should().BeTrue();
        body.TryGetProperty("totalRevenue", out _).Should().BeFalse();
        body.TryGetProperty("monthlyRevenue", out _).Should().BeFalse();
    }

    /// <summary>
    /// The open ticket count reflects only <see cref="TicketStatus.Open"/> tickets, as counted by
    /// <c>GetDashboardStatsHandler</c>.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task OpenTicketsCountsOnlyOpenStatusAsync()
    {
        async Task<int> OpenTicketsCountAsync()
        {
            var admin = await AdminTestSeeds.AdminClientAsync(factory);
            var response = await admin.GetAsync("/api/admin/dashboard/stats");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            return body.GetProperty("openTickets").GetInt32();
        }

        var before = await OpenTicketsCountAsync();

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var client = Client.Create($"user-{Guid.NewGuid():N}", "Ada", "Lovelace", $"ada-{Guid.NewGuid():N}@example.test", TestMoments.Seeded);
        context.Clients.Add(client);
        await context.SaveChangesAsync(CancellationToken.None);
        var departmentId = await SupportTestSeeds.SeedDepartmentAsync(context);
        var openTicket = Ticket.Create(client.Id, "Open one", "Help", departmentId, TicketPriority.Medium, TestMoments.Seeded);
        var closedTicket = Ticket.Create(client.Id, "Closed one", "Help", departmentId, TicketPriority.Medium, TestMoments.Seeded);
        closedTicket.Close(TestMoments.Seeded);
        context.Tickets.Add(openTicket);
        context.Tickets.Add(closedTicket);
        await context.SaveChangesAsync(CancellationToken.None);

        var after = await OpenTicketsCountAsync();

        after.Should().Be(before + 1);
    }

    /// <summary>A caller with only the Client role is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToTheClientRoleAsync()
    {
        var client = await AdminTestSeeds.ClientRoleClientAsync(factory, "dashboard-stats-forbidden");

        var response = await client.GetAsync("/api/admin/dashboard/stats");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>The dashboard is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().GetAsync("/api/admin/dashboard/stats");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
