using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Orders;
using Innovayse.Hostpanel.Domain.Support;
using Innovayse.Hostpanel.Domain.Support.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Admin;

/// <summary>
/// <c>GET /api/admin/dashboard/charts</c>: the series the dashboard charts draw. Every day of the period is
/// present, income is summed per day in the host time zone and compared with the period before, the other
/// series count what the period holds, and the route is closed to anyone who is not staff. Each test runs on
/// its own clocked host in its own year so no row of another test falls inside its window.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetDashboardChartsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The route under test.</summary>
    private const string Route = "/api/admin/dashboard/charts";

    /// <summary>Every period answers one entry per day on every daily series, the last one being today.</summary>
    /// <param name="period">The period name sent.</param>
    /// <param name="days">How many days it covers.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("Days30", 30)]
    [InlineData("Days90", 90)]
    public async Task AnswersOneEntryPerDayOfThePeriodAsync(string period, int days)
    {
        using var host = new ClockedHost(factory, At(2040, 6, 15, 12));
        var admin = await AdminClientAsync(host);

        var body = await admin.GetFromJsonAsync<JsonElement>($"{Route}?period={period}");

        foreach (var series in new[] { body.GetProperty("income").GetProperty("days"), body.GetProperty("newOrders"), body.GetProperty("newClients") })
        {
            series.GetArrayLength().Should().Be(days);
            series[days - 1].GetProperty("date").GetString().Should().Be("2040-06-15");
        }

        body.GetProperty("income").GetProperty("days")[0].GetProperty("date").GetString()
            .Should().Be(new DateOnly(2040, 6, 15).AddDays(1 - days).ToString("yyyy-MM-dd"));
        body.GetProperty("income").GetProperty("total").GetDecimal().Should().Be(0m);
        body.GetProperty("income").GetProperty("changePercent").ValueKind.Should().Be(JsonValueKind.Null);
    }

    /// <summary>An omitted period is the 30 days.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DefaultsToThirtyDaysAsync()
    {
        using var host = new ClockedHost(factory, At(2040, 6, 15, 12));
        var admin = await AdminClientAsync(host);

        var body = await admin.GetFromJsonAsync<JsonElement>(Route);

        body.GetProperty("newClients").GetArrayLength().Should().Be(30);
    }

    /// <summary>A period that is not offered is refused with 400, by name or by number.</summary>
    /// <param name="period">The period sent.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("Days45")]
    [InlineData("45")]
    [InlineData("0")]
    public async Task RefusesAPeriodThatIsNotOfferedAsync(string period)
    {
        var admin = await AdminTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"{Route}?period={period}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Income is summed per day, the total and the daily average cover the whole period, and the change is
    /// measured against the period of the same length right before it.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SumsIncomePerDayAndComparesItWithThePreviousPeriodAsync()
    {
        using var host = new ClockedHost(factory, At(2041, 6, 15, 12));
        await SeedPaidInvoiceAsync(host, 100m, At(2041, 6, 10, 10));
        await SeedPaidInvoiceAsync(host, 50m, At(2041, 6, 10, 23));
        await SeedPaidInvoiceAsync(host, 100m, At(2041, 5, 1, 9));
        var admin = await AdminClientAsync(host);

        var income = (await admin.GetFromJsonAsync<JsonElement>($"{Route}?period=Days30")).GetProperty("income");

        income.GetProperty("total").GetDecimal().Should().Be(150m);
        income.GetProperty("previousTotal").GetDecimal().Should().Be(100m);
        income.GetProperty("changePercent").GetDecimal().Should().Be(50m);
        income.GetProperty("dailyAverage").GetDecimal().Should().Be(5m);
        var days = income.GetProperty("days").EnumerateArray().ToDictionary(d => d.GetProperty("date").GetString()!, d => d.GetProperty("amount").GetDecimal());
        days["2041-06-10"].Should().Be(150m);
        days["2041-06-09"].Should().Be(0m);
        days.Values.Sum().Should().Be(150m);
    }

    /// <summary>Income of the 90 days reaches back into what the 30 days call the previous period.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ReadsTheLongerPeriodFurtherBackAsync()
    {
        using var host = new ClockedHost(factory, At(2045, 6, 15, 12));
        await SeedPaidInvoiceAsync(host, 80m, At(2045, 5, 1, 9));
        var admin = await AdminClientAsync(host);

        var short30 = (await admin.GetFromJsonAsync<JsonElement>($"{Route}?period=Days30")).GetProperty("income");
        var long90 = (await admin.GetFromJsonAsync<JsonElement>($"{Route}?period=Days90")).GetProperty("income");

        short30.GetProperty("total").GetDecimal().Should().Be(0m);
        short30.GetProperty("previousTotal").GetDecimal().Should().Be(80m);
        long90.GetProperty("total").GetDecimal().Should().Be(80m);
    }

    /// <summary>A day is cut by the host time zone: a payment late in the evening UTC belongs to the next local day.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CutsDaysByTheHostTimeZoneAsync()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("Host", TimeSpan.FromHours(4), "Host", "Host");
        using var host = new ClockedHost(factory, At(2044, 6, 15, 22), zone);
        await SeedPaidInvoiceAsync(host, 100m, At(2044, 6, 15, 21));
        var admin = await AdminClientAsync(host);

        var income = (await admin.GetFromJsonAsync<JsonElement>($"{Route}?period=Days30")).GetProperty("income");

        var days = income.GetProperty("days").EnumerateArray().ToDictionary(d => d.GetProperty("date").GetString()!, d => d.GetProperty("amount").GetDecimal());
        days.Keys.Last().Should().Be("2044-06-16");
        days["2044-06-16"].Should().Be(100m);
        days["2044-06-15"].Should().Be(0m);
    }

    /// <summary>
    /// Invoices raised in the period are counted and added up per status in the order paid, unpaid, overdue,
    /// cancelled; one raised before the period is left out, and a status nobody holds answers zero.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CountsInvoicesRaisedInThePeriodByStatusAsync()
    {
        using var host = new ClockedHost(factory, At(2042, 6, 15, 12));
        await SeedPaidInvoiceAsync(host, 100m, At(2042, 6, 12, 9));
        await SeedInvoiceAsync(host, 40m, At(2042, 6, 12, 9), invoice => { });
        await SeedInvoiceAsync(host, 25m, At(2042, 6, 13, 9), invoice => invoice.MarkOverdue(At(2042, 6, 14, 9)));
        await SeedInvoiceAsync(host, 10m, At(2042, 6, 13, 9), invoice => invoice.Cancel(At(2042, 6, 14, 9)));
        await SeedInvoiceAsync(host, 999m, At(2042, 2, 1, 9), invoice => { });
        var admin = await AdminClientAsync(host);

        var body = await admin.GetFromJsonAsync<JsonElement>($"{Route}?period=Days30");

        var statuses = body.GetProperty("invoicesByStatus").EnumerateArray().ToList();
        statuses.Select(s => s.GetProperty("status").GetString()).Should().Equal("Paid", "Unpaid", "Overdue", "Cancelled");
        statuses.Select(s => s.GetProperty("count").GetInt32()).Should().Equal(1, 1, 1, 1);
        statuses.Select(s => s.GetProperty("amount").GetDecimal()).Should().Equal(100m, 40m, 25m, 10m);
    }

    /// <summary>New orders and new clients are counted on the day they were created; a cancelled order and anything outside the period are not.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CountsNewOrdersAndNewClientsPerDayAsync()
    {
        using var host = new ClockedHost(factory, At(2043, 6, 15, 12));
        await SeedClientAsync(host, At(2043, 6, 12, 9));
        await SeedClientAsync(host, At(2043, 6, 12, 18));
        var oldClient = await SeedClientAsync(host, At(2043, 5, 1, 9));
        await SeedOrderAsync(host, oldClient, At(2043, 6, 14, 9), cancel: false);
        await SeedOrderAsync(host, oldClient, At(2043, 6, 14, 10), cancel: true);
        await SeedOrderAsync(host, oldClient, At(2043, 5, 1, 9), cancel: false);
        var admin = await AdminClientAsync(host);

        var short30 = await admin.GetFromJsonAsync<JsonElement>($"{Route}?period=Days30");
        var long90 = await admin.GetFromJsonAsync<JsonElement>($"{Route}?period=Days90");

        CountOn(short30.GetProperty("newClients"), "2043-06-12").Should().Be(2);
        CountOn(short30.GetProperty("newOrders"), "2043-06-14").Should().Be(1);
        Total(short30.GetProperty("newClients")).Should().Be(2);
        Total(short30.GetProperty("newOrders")).Should().Be(1);
        Total(long90.GetProperty("newClients")).Should().Be(3);
        Total(long90.GetProperty("newOrders")).Should().Be(2);
    }

    /// <summary>Open tickets are counted per priority, highest first, every priority present; a closed ticket is not counted.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CountsOpenTicketsByPriorityAsync()
    {
        var admin = await AdminTestSeeds.AdminClientAsync(factory);
        var before = await OpenTicketsAsync(admin);

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var client = Client.Create($"user-{Guid.NewGuid():N}", "Ada", "Lovelace", $"ada-{Guid.NewGuid():N}@example.test", TestMoments.Seeded);
            context.Clients.Add(client);
            await context.SaveChangesAsync(CancellationToken.None);
            var departmentId = await SupportTestSeeds.SeedDepartmentAsync(context);
            var closed = Ticket.Create(client.Id, "Closed", "Help", departmentId, TicketPriority.High, TestMoments.Seeded);
            closed.Close(TestMoments.Seeded);
            context.Tickets.AddRange(
                Ticket.Create(client.Id, "Urgent", "Help", departmentId, TicketPriority.High, TestMoments.Seeded),
                Ticket.Create(client.Id, "Slow", "Help", departmentId, TicketPriority.Low, TestMoments.Seeded),
                closed);
            await context.SaveChangesAsync(CancellationToken.None);
        }

        var after = await OpenTicketsAsync(admin);

        after.Select(p => p.Priority).Should().Equal("High", "Medium", "Low");
        after.Single(p => p.Priority == "High").Count.Should().Be(before.Single(p => p.Priority == "High").Count + 1);
        after.Single(p => p.Priority == "Medium").Count.Should().Be(before.Single(p => p.Priority == "Medium").Count);
        after.Single(p => p.Priority == "Low").Count.Should().Be(before.Single(p => p.Priority == "Low").Count + 1);
    }

    /// <summary>A caller with only the Client role is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToTheClientRoleAsync()
    {
        var client = await AdminTestSeeds.ClientRoleClientAsync(factory, "dashboard-charts-forbidden");

        var response = await client.GetAsync(Route);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().GetAsync(Route);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Builds a UTC moment.</summary>
    /// <param name="year">The year.</param>
    /// <param name="month">The month.</param>
    /// <param name="day">The day.</param>
    /// <param name="hour">The hour.</param>
    /// <returns>The moment.</returns>
    private static DateTimeOffset At(int year, int month, int day, int hour) => new(year, month, day, hour, 0, 0, TimeSpan.Zero);

    /// <summary>Reads the count of one day of a count series.</summary>
    /// <param name="series">The series.</param>
    /// <param name="date">The day, as <c>yyyy-MM-dd</c>.</param>
    /// <returns>The count on that day.</returns>
    private static int CountOn(JsonElement series, string date) =>
        series.EnumerateArray().Single(d => d.GetProperty("date").GetString() == date).GetProperty("count").GetInt32();

    /// <summary>Adds up a count series.</summary>
    /// <param name="series">The series.</param>
    /// <returns>The sum of its counts.</returns>
    private static int Total(JsonElement series) => series.EnumerateArray().Sum(d => d.GetProperty("count").GetInt32());

    /// <summary>Reads the open tickets by priority the endpoint answers.</summary>
    /// <param name="admin">An admin client.</param>
    /// <returns>The priority names with their counts.</returns>
    private static async Task<List<(string Priority, int Count)>> OpenTicketsAsync(HttpClient admin)
    {
        var body = await admin.GetFromJsonAsync<JsonElement>(Route);
        return [.. body.GetProperty("openTickets").EnumerateArray().Select(p => (p.GetProperty("priority").GetString()!, p.GetProperty("count").GetInt32()))];
    }

    /// <summary>A client for the clocked host carrying the seeded admin's bearer token.</summary>
    /// <param name="host">The clocked host.</param>
    /// <returns>An authenticated client.</returns>
    private async Task<HttpClient> AdminClientAsync(ClockedHost host)
    {
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await factory.GetAdminTokenAsync());
        return client;
    }

    /// <summary>Creates a client created at a moment.</summary>
    /// <param name="host">The host to seed against.</param>
    /// <param name="createdAt">When the account was created.</param>
    /// <returns>The client id.</returns>
    private static async Task<int> SeedClientAsync(ClockedHost host, DateTimeOffset createdAt)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var client = Client.Create($"user-{Guid.NewGuid():N}", "Ada", "Lovelace", $"ada-{Guid.NewGuid():N}@example.test", createdAt);
        client.Backdate(createdAt);
        context.Clients.Add(client);
        await context.SaveChangesAsync(CancellationToken.None);
        return client.Id;
    }

    /// <summary>Creates an order placed at a moment.</summary>
    /// <param name="host">The host to seed against.</param>
    /// <param name="clientId">The ordering client.</param>
    /// <param name="createdAt">When the order was placed.</param>
    /// <param name="cancel">Whether the order is then cancelled.</param>
    /// <returns>A task that completes when the order is saved.</returns>
    private static async Task SeedOrderAsync(ClockedHost host, int clientId, DateTimeOffset createdAt, bool cancel)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var order = Order.Create($"ORD-{Guid.NewGuid():N}"[..12], clientId, "bank_transfer", ipAddress: null, currency: "USD", now: createdAt);
        order.Backdate(createdAt);
        if (cancel)
        {
            order.Cancel();
        }

        context.Orders.Add(order);
        await context.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>Creates an invoice in the base currency, raised at a moment, and lets the caller move it on.</summary>
    /// <param name="host">The host to seed against.</param>
    /// <param name="amount">The invoice total.</param>
    /// <param name="raisedAt">When the invoice was raised.</param>
    /// <param name="advance">What happens to the invoice before it is saved.</param>
    /// <returns>A task that completes when the invoice is saved.</returns>
    private static async Task SeedInvoiceAsync(ClockedHost host, decimal amount, DateTimeOffset raisedAt, Action<Invoice> advance)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var baseCurrency = await context.Currencies.SingleAsync(c => c.IsBase, CancellationToken.None);
        var client = Client.Create($"user-{Guid.NewGuid():N}", "Ada", "Lovelace", $"ada-{Guid.NewGuid():N}@example.test", raisedAt);
        context.Clients.Add(client);
        await context.SaveChangesAsync(CancellationToken.None);

        var invoice = Invoice.Create(client.Id, raisedAt.AddDays(14), baseCurrency.Code, raisedAt);
        invoice.Backdate(raisedAt);
        invoice.AddItem("Hosting", amount, 1);
        advance(invoice);
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>Creates an invoice in the base currency that is paid in full at the moment it was raised.</summary>
    /// <param name="host">The host to seed against.</param>
    /// <param name="amount">The invoice total, paid in full.</param>
    /// <param name="paidAt">When it was raised and paid.</param>
    /// <returns>A task that completes when the invoice is saved.</returns>
    private static Task SeedPaidInvoiceAsync(ClockedHost host, decimal amount, DateTimeOffset paidAt) =>
        SeedInvoiceAsync(host, amount, paidAt, invoice =>
            invoice.AddPayment(paidAt, "Card", $"tx-{Guid.NewGuid():N}", amount, 0m, null, TransactionSource.Manual));
}
