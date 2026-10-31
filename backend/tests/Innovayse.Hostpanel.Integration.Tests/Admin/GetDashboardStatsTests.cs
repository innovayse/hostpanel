using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Support;
using Innovayse.Hostpanel.Domain.Support.Enums;
using Microsoft.Extensions.DependencyInjection;
using Innovayse.Hostpanel.Domain.Billing.Enums;

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
    /// <summary>Dashboard stats answer 200 with every counter present.</summary>
    [Fact]
    public async Task ReturnsEveryCounterAsync()
    {
        var client = await AdminTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/admin/dashboard/stats");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.TryGetProperty("totalRevenue", out _).Should().BeTrue();
        body.TryGetProperty("totalClients", out _).Should().BeTrue();
    }

    /// <summary>
    /// The open ticket count reflects only <see cref="TicketStatus.Open"/> tickets, as counted by
    /// <c>GetDashboardStatsHandler</c>.
    /// </summary>
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
        var client = Client.Create($"user-{Guid.NewGuid():N}", "Ada", "Lovelace", $"ada-{Guid.NewGuid():N}@example.test", DateTimeOffset.UtcNow);
        context.Clients.Add(client);
        await context.SaveChangesAsync(CancellationToken.None);
        var departmentId = await SupportTestSeeds.SeedDepartmentAsync(context);
        var openTicket = Ticket.Create(client.Id, "Open one", "Help", departmentId, TicketPriority.Medium, DateTimeOffset.UtcNow);
        var closedTicket = Ticket.Create(client.Id, "Closed one", "Help", departmentId, TicketPriority.Medium, DateTimeOffset.UtcNow);
        closedTicket.Close(DateTimeOffset.UtcNow);
        context.Tickets.Add(openTicket);
        context.Tickets.Add(closedTicket);
        await context.SaveChangesAsync(CancellationToken.None);

        var after = await OpenTicketsCountAsync();

        after.Should().Be(before + 1);
    }

    /// <summary>A caller with only the Client role is refused with 403.</summary>
    [Fact]
    public async Task IsClosedToTheClientRoleAsync()
    {
        var client = await AdminTestSeeds.ClientRoleClientAsync(factory, "dashboard-stats-forbidden");

        var response = await client.GetAsync("/api/admin/dashboard/stats");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>The dashboard is not for anonymous callers.</summary>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().GetAsync("/api/admin/dashboard/stats");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Total revenue is income as the reports read it: the scenario adds 390 (payments less the
    /// partial refund, the imported history, the converted foreign payment, no credit), all of it
    /// long before the last 30 days, so the monthly figure does not move.
    /// </summary>
    [Fact]
    public async Task RevenueIsIncomeNetOfRefundsAsync()
    {
        var admin = await AdminTestSeeds.AdminClientAsync(factory);
        var before = await admin.GetFromJsonAsync<JsonElement>("/api/admin/dashboard/stats");

        var scenario = await IncomeTestSeeds.SeedAsync(factory, 2019);
        var after = await admin.GetFromJsonAsync<JsonElement>("/api/admin/dashboard/stats");

        (after.GetProperty("totalRevenue").GetDecimal() - before.GetProperty("totalRevenue").GetDecimal()).Should().Be(390m);
        after.GetProperty("monthlyRevenue").GetDecimal().Should().Be(before.GetProperty("monthlyRevenue").GetDecimal());
        after.GetProperty("currency").GetString().Should().Be(scenario.BaseCurrency);
    }

    /// <summary>
    /// Revenue adds invoices up in the base currency: a paid invoice in a currency worth half the base adds half
    /// its total, not the whole of it, so a mixed-currency demo does not report a thousand times its real revenue.
    /// </summary>
    [Fact]
    public async Task RevenueIsCountedInTheBaseCurrencyAsync()
    {
        async Task<decimal> TotalRevenueAsync()
        {
            var admin = await AdminTestSeeds.AdminClientAsync(factory);
            var response = await admin.GetAsync("/api/admin/dashboard/stats");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            return body.GetProperty("totalRevenue").GetDecimal();
        }

        var before = await TotalRevenueAsync();

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            context.Currencies.Add(Currency.Create("ZQD", "999", string.Empty, " ZQD", 2, 0.5m, isBase: false));
            var client = Client.Create($"user-{Guid.NewGuid():N}", "Ada", "Lovelace", $"ada-{Guid.NewGuid():N}@example.test", DateTimeOffset.UtcNow);
            context.Clients.Add(client);
            await context.SaveChangesAsync(CancellationToken.None);

            var invoice = Invoice.Create(client.Id, DateTimeOffset.UtcNow.AddDays(14), "ZQD", DateTimeOffset.UtcNow);
            invoice.AddItem("Hosting", 100m, 1);
            invoice.AddPayment(DateTimeOffset.UtcNow, "Card", $"tx-{Guid.NewGuid():N}", 100m, 0m, null, TransactionSource.Manual);
            context.Invoices.Add(invoice);
            await context.SaveChangesAsync(CancellationToken.None);
        }

        var after = await TotalRevenueAsync();

        (after - before).Should().Be(50m);
    }
}
