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
using Innovayse.Hostpanel.Shared.Models;
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

    /// <summary>A period that is not offered is refused with 400 and the one error body, by name or by number.</summary>
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
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        body.Should().NotBeNull();
        body!.Status.Should().Be((int)HttpStatusCode.BadRequest);
        body.Type.Should().NotBeNullOrWhiteSpace();
        body.Detail.Should().NotBeNullOrWhiteSpace();
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
        await SeedOrderAsync(host, oldClient, At(2043, 6, 14, 9));
        await SeedOrderAsync(host, oldClient, At(2043, 6, 14, 10), order => order.Cancel());
        await SeedOrderAsync(host, oldClient, At(2043, 5, 1, 9));
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

    /// <summary>
    /// A payment less its refund is income on the day each happened, a partially refunded invoice is still a paid one
    /// on the status chart, and an invoice refunded in full in the period before nets to nothing there.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CountsRefundsAndPartiallyRefundedInvoicesAsync()
    {
        using var host = new ClockedHost(factory, At(2046, 6, 15, 12));
        await SeedRefundedInvoiceAsync(host, 200m, At(2046, 6, 10, 10), 50m, At(2046, 6, 12, 10));
        await SeedRefundedInvoiceAsync(host, 100m, At(2046, 5, 2, 10), 100m, At(2046, 5, 3, 10));
        var admin = await AdminClientAsync(host);

        var body = await admin.GetFromJsonAsync<JsonElement>($"{Route}?period=Days30");

        var income = body.GetProperty("income");
        var days = income.GetProperty("days").EnumerateArray().ToDictionary(d => d.GetProperty("date").GetString()!, d => d.GetProperty("amount").GetDecimal());
        days["2046-06-10"].Should().Be(200m);
        days["2046-06-12"].Should().Be(-50m);
        income.GetProperty("total").GetDecimal().Should().Be(150m);
        income.GetProperty("previousTotal").GetDecimal().Should().Be(0m);
        income.GetProperty("changePercent").ValueKind.Should().Be(JsonValueKind.Null);
        var paid = body.GetProperty("invoicesByStatus").EnumerateArray().Single(s => s.GetProperty("status").GetString() == "Paid");
        paid.GetProperty("count").GetInt32().Should().Be(1);
        paid.GetProperty("amount").GetDecimal().Should().Be(200m);
    }

    /// <summary>
    /// Income in another currency is summed per currency and converted once, for the period and the one before alike:
    /// three payments worth a fraction of a cent each round to nothing one by one, and to a cent together.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ConvertsEachCurrencyOnceForBothPeriodsAsync()
    {
        await EnsureForeignCurrencyAsync();
        using var host = new ClockedHost(factory, At(2047, 6, 15, 12));
        await SeedPaidInvoiceAsync(host, 10m, At(2047, 6, 10, 10));
        foreach (var day in new[] { 11, 12, 13 })
        {
            await SeedPaidInvoiceAsync(host, 1m, At(2047, 6, day, 10), IncomeTestSeeds.ForeignCurrency);
        }

        foreach (var day in new[] { 1, 2, 3 })
        {
            await SeedPaidInvoiceAsync(host, 1m, At(2047, 5, day, 10), IncomeTestSeeds.ForeignCurrency);
        }

        var admin = await AdminClientAsync(host);

        var income = (await admin.GetFromJsonAsync<JsonElement>($"{Route}?period=Days30")).GetProperty("income");

        income.GetProperty("total").GetDecimal().Should().Be(10.01m);
        income.GetProperty("previousTotal").GetDecimal().Should().Be(0.01m);
        var days = income.GetProperty("days").EnumerateArray().ToDictionary(d => d.GetProperty("date").GetString()!, d => d.GetProperty("amount").GetDecimal());
        days["2047-06-10"].Should().Be(10m);
        days["2047-06-11"].Should().Be(0m);
    }

    /// <summary>Two currencies on one day are each worth their rate: a foreign payment adds its converted amount, not its face value.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AddsTwoCurrenciesInTheBaseCurrencyAsync()
    {
        await EnsureForeignCurrencyAsync();
        using var host = new ClockedHost(factory, At(2051, 6, 15, 12));
        await SeedPaidInvoiceAsync(host, 100m, At(2051, 6, 10, 9));
        await SeedPaidInvoiceAsync(host, 40_000m, At(2051, 6, 10, 11), IncomeTestSeeds.ForeignCurrency);
        var admin = await AdminClientAsync(host);

        var body = await admin.GetFromJsonAsync<JsonElement>($"{Route}?period=Days30");

        body.GetProperty("income").GetProperty("total").GetDecimal().Should().Be(200m);
        var days = body.GetProperty("income").GetProperty("days").EnumerateArray().ToDictionary(d => d.GetProperty("date").GetString()!, d => d.GetProperty("amount").GetDecimal());
        days["2051-06-10"].Should().Be(200m);
        var paid = body.GetProperty("invoicesByStatus").EnumerateArray().Single(s => s.GetProperty("status").GetString() == "Paid");
        paid.GetProperty("count").GetInt32().Should().Be(2);
        paid.GetProperty("amount").GetDecimal().Should().Be(200m);
    }

    /// <summary>A fraud order and a cancelled one sold nothing, so neither is a new order.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task LeavesFraudAndCancelledOrdersOutOfNewOrdersAsync()
    {
        using var host = new ClockedHost(factory, At(2052, 6, 15, 12));
        var client = await SeedClientAsync(host, At(2052, 5, 1, 9));
        await SeedOrderAsync(host, client, At(2052, 6, 14, 9));
        await SeedOrderAsync(host, client, At(2052, 6, 14, 10), order => order.MarkFraud());
        await SeedOrderAsync(host, client, At(2052, 6, 14, 11), order => order.Cancel());
        var admin = await AdminClientAsync(host);

        var body = await admin.GetFromJsonAsync<JsonElement>($"{Route}?period=Days30");

        CountOn(body.GetProperty("newOrders"), "2052-06-14").Should().Be(1);
        Total(body.GetProperty("newOrders")).Should().Be(1);
    }

    /// <summary>New orders and new clients are cut by the host time zone: a record made late in the evening UTC belongs to the next local day.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CutsOrdersAndClientsByTheHostTimeZoneAsync()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("Host", TimeSpan.FromHours(4), "Host", "Host");
        using var host = new ClockedHost(factory, At(2049, 6, 15, 22), zone);
        var early = await SeedClientAsync(host, At(2049, 6, 15, 19));
        await SeedClientAsync(host, At(2049, 6, 15, 21));
        await SeedOrderAsync(host, early, At(2049, 6, 15, 19));
        await SeedOrderAsync(host, early, At(2049, 6, 15, 21));
        var admin = await AdminClientAsync(host);

        var body = await admin.GetFromJsonAsync<JsonElement>($"{Route}?period=Days30");

        CountOn(body.GetProperty("newClients"), "2049-06-15").Should().Be(1);
        CountOn(body.GetProperty("newClients"), "2049-06-16").Should().Be(1);
        CountOn(body.GetProperty("newOrders"), "2049-06-15").Should().Be(1);
        CountOn(body.GetProperty("newOrders"), "2049-06-16").Should().Be(1);
    }

    /// <summary>A zone whose offset is not a whole hour still cuts days at its own midnight.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CutsDaysByAZoneWithAHalfHourOffsetAsync()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("Host", new TimeSpan(5, 30, 0), "Host", "Host");
        using var host = new ClockedHost(factory, At(2048, 6, 16, 12), zone);
        await SeedClientAsync(host, new DateTimeOffset(2048, 6, 15, 18, 15, 0, TimeSpan.Zero));
        await SeedClientAsync(host, new DateTimeOffset(2048, 6, 15, 18, 45, 0, TimeSpan.Zero));
        await SeedPaidInvoiceAsync(host, 70m, new DateTimeOffset(2048, 6, 15, 18, 45, 0, TimeSpan.Zero));
        var admin = await AdminClientAsync(host);

        var body = await admin.GetFromJsonAsync<JsonElement>($"{Route}?period=Days30");

        // The seeded paid invoice also created its client at 18:45 UTC, so 16 June holds that one and the one seeded there.
        CountOn(body.GetProperty("newClients"), "2048-06-15").Should().Be(1);
        CountOn(body.GetProperty("newClients"), "2048-06-16").Should().Be(2);
        var days = body.GetProperty("income").GetProperty("days").EnumerateArray().ToDictionary(d => d.GetProperty("date").GetString()!, d => d.GetProperty("amount").GetDecimal());
        days["2048-06-15"].Should().Be(0m);
        days["2048-06-16"].Should().Be(70m);
    }

    /// <summary>The invoices of the status chart are the ones raised inside the days of the host time zone, not of UTC.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CutsTheInvoiceWindowByTheHostTimeZoneAsync()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("Host", TimeSpan.FromHours(4), "Host", "Host");
        using var host = new ClockedHost(factory, At(2050, 6, 15, 22), zone);

        // The window starts at the local midnight of 18 May, 20:00 UTC the evening before.
        await SeedInvoiceAsync(host, 11m, new DateTimeOffset(2050, 5, 17, 19, 30, 0, TimeSpan.Zero), invoice => { });
        await SeedInvoiceAsync(host, 22m, new DateTimeOffset(2050, 5, 17, 20, 30, 0, TimeSpan.Zero), invoice => { });
        var admin = await AdminClientAsync(host);

        var body = await admin.GetFromJsonAsync<JsonElement>($"{Route}?period=Days30");

        var unpaid = body.GetProperty("invoicesByStatus").EnumerateArray().Single(s => s.GetProperty("status").GetString() == "Unpaid");
        unpaid.GetProperty("count").GetInt32().Should().Be(1);
        unpaid.GetProperty("amount").GetDecimal().Should().Be(22m);
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
    /// <param name="advance">What happens to the order before it is saved; nothing when omitted.</param>
    /// <returns>A task that completes when the order is saved.</returns>
    private static async Task SeedOrderAsync(ClockedHost host, int clientId, DateTimeOffset createdAt, Action<Order>? advance = null)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var order = Order.Create($"ORD-{Guid.NewGuid():N}"[..12], clientId, "bank_transfer", ipAddress: null, currency: "USD", now: createdAt);
        order.Backdate(createdAt);
        advance?.Invoke(order);

        context.Orders.Add(order);
        await context.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>Creates an invoice raised at a moment and lets the caller move it on.</summary>
    /// <param name="host">The host to seed against.</param>
    /// <param name="amount">The invoice total.</param>
    /// <param name="raisedAt">When the invoice was raised.</param>
    /// <param name="advance">What happens to the invoice before it is saved.</param>
    /// <param name="currency">The currency it bills in; the base currency when omitted.</param>
    /// <returns>A task that completes when the invoice is saved.</returns>
    private static async Task SeedInvoiceAsync(
        ClockedHost host, decimal amount, DateTimeOffset raisedAt, Action<Invoice> advance, string? currency = null)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var code = currency ?? (await context.Currencies.SingleAsync(c => c.IsBase, CancellationToken.None)).Code;
        var client = Client.Create($"user-{Guid.NewGuid():N}", "Ada", "Lovelace", $"ada-{Guid.NewGuid():N}@example.test", raisedAt);
        context.Clients.Add(client);
        await context.SaveChangesAsync(CancellationToken.None);

        var invoice = Invoice.Create(client.Id, raisedAt.AddDays(14), code, raisedAt);
        invoice.Backdate(raisedAt);
        invoice.AddItem("Hosting", amount, 1);
        advance(invoice);
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>Creates an invoice that is paid in full at the moment it was raised.</summary>
    /// <param name="host">The host to seed against.</param>
    /// <param name="amount">The invoice total, paid in full.</param>
    /// <param name="paidAt">When it was raised and paid.</param>
    /// <param name="currency">The currency it bills in; the base currency when omitted.</param>
    /// <returns>A task that completes when the invoice is saved.</returns>
    private static Task SeedPaidInvoiceAsync(ClockedHost host, decimal amount, DateTimeOffset paidAt, string? currency = null) =>
        SeedInvoiceAsync(
            host,
            amount,
            paidAt,
            invoice => invoice.AddPayment(paidAt, "Card", $"tx-{Guid.NewGuid():N}", amount, 0m, null, TransactionSource.Manual),
            currency);

    /// <summary>Creates an invoice paid in full and then refunded in part or in full.</summary>
    /// <param name="host">The host to seed against.</param>
    /// <param name="amount">The invoice total, paid in full.</param>
    /// <param name="paidAt">When it was raised and paid.</param>
    /// <param name="refunded">The amount given back.</param>
    /// <param name="refundedAt">When the refund was made.</param>
    /// <returns>A task that completes when the invoice is saved.</returns>
    private static Task SeedRefundedInvoiceAsync(ClockedHost host, decimal amount, DateTimeOffset paidAt, decimal refunded, DateTimeOffset refundedAt) =>
        SeedInvoiceAsync(host, amount, paidAt, invoice =>
        {
            invoice.AddPayment(paidAt, "Card", $"tx-{Guid.NewGuid():N}", amount, 0m, null, TransactionSource.Manual);
            invoice.AddRefund(refundedAt, "Card", $"rf-{Guid.NewGuid():N}", refunded, 0m, null, false, TransactionSource.Manual);
        });

    /// <summary>Makes sure the foreign currency exists at the rate the scenarios convert it by.</summary>
    /// <returns>A task that completes when the currency is saved.</returns>
    private async Task EnsureForeignCurrencyAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var foreign = await context.Currencies.FirstOrDefaultAsync(c => c.Code == IncomeTestSeeds.ForeignCurrency, CancellationToken.None);
        if (foreign is null)
        {
            context.Currencies.Add(Currency.Create(
                IncomeTestSeeds.ForeignCurrency, "051", string.Empty, " " + IncomeTestSeeds.ForeignCurrency, 2, IncomeTestSeeds.ForeignRate, isBase: false));
        }
        else
        {
            foreign.UpdateRate(IncomeTestSeeds.ForeignRate);
        }

        await context.SaveChangesAsync(CancellationToken.None);
    }
}
