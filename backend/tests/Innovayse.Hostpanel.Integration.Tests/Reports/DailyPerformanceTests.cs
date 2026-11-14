using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Reports;

/// <summary>
/// <c>GET /api/reports/daily-performance</c> against Postgres: one row per day in the range the
/// admin asks for.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class DailyPerformanceTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The fixed moment the host clock stands at, so "today" never moves under a test.</summary>
    private static readonly DateTimeOffset Now = new(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A window with no activity still answers with one zero-filled row per day.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ReturnsOneRowPerDay()
    {
        using var host = new ClockedHost(factory, Now);
        var client = await AdminClientAsync(host);
        var to = DateOnly.FromDateTime(Now.UtcDateTime);
        var from = to.AddDays(-2);

        var response = await client.GetAsync($"/api/reports/daily-performance?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var rows = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
        rows.Should().HaveCount(3);
    }

    /// <summary>A start after the end is a malformed range, not a crash.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnInvertedRange()
    {
        using var host = new ClockedHost(factory, Now);
        var client = await AdminClientAsync(host);
        var today = DateOnly.FromDateTime(Now.UtcDateTime);

        var response = await client.GetAsync($"/api/reports/daily-performance?from={today:yyyy-MM-dd}&to={today.AddDays(-5):yyyy-MM-dd}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// An invoice counts as paid on the day it was paid, the last day of the range counts in full,
    /// and only an accepted order is a completed one.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CountsPaidInvoicesByPaidDayAndOnlyAcceptedOrders()
    {
        await IncomeTestSeeds.SeedAsync(factory, 2036);
        var client = await ReportsTestSeeds.AdminClientAsync(factory);

        var boundary = (await client.GetFromJsonAsync<List<JsonElement>>("/api/reports/daily-performance?from=2036-01-31&to=2036-02-01"))!;
        var orders = (await client.GetFromJsonAsync<List<JsonElement>>("/api/reports/daily-performance?from=2036-05-03&to=2036-05-21"))!;

        boundary.Select(r => r.GetProperty("paidInvoices").GetInt32()).Should().Equal(1, 1);
        orders.Sum(r => r.GetProperty("completedOrders").GetInt32()).Should().Be(1);
        orders.Single(r => r.GetProperty("date").GetString() == "2036-05-20").GetProperty("completedOrders").GetInt32().Should().Be(1);
    }

    /// <summary>
    /// A day is the host time zone's: an invoice raised late in the evening UTC counts on the next local day, the range
    /// defaults to today of that zone, and a zone whose offset is not a whole hour cuts at its own midnight.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CutsDaysByTheHostTimeZone()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("Host", new TimeSpan(5, 30, 0), "Host", "Host");
        using var host = new ClockedHost(factory, new DateTimeOffset(2053, 6, 15, 20, 0, 0, TimeSpan.Zero), zone);
        await SeedInvoiceAsync(host, new DateTimeOffset(2053, 6, 15, 18, 15, 0, TimeSpan.Zero));
        await SeedInvoiceAsync(host, new DateTimeOffset(2053, 6, 15, 18, 45, 0, TimeSpan.Zero));
        var client = await AdminClientAsync(host);

        var rows = (await client.GetFromJsonAsync<List<JsonElement>>("/api/reports/daily-performance?from=2053-06-15&to=2053-06-16"))!;
        var byDefault = (await client.GetFromJsonAsync<List<JsonElement>>("/api/reports/daily-performance"))!;

        rows.Select(r => r.GetProperty("newInvoices").GetInt32()).Should().Equal(1, 1);
        byDefault.Should().HaveCount(30);
        byDefault[^1].GetProperty("date").GetString().Should().Be("2053-06-16");
    }

    /// <summary>Raises an invoice at a moment.</summary>
    /// <param name="host">The host to seed against.</param>
    /// <param name="raisedAt">When the invoice was raised.</param>
    /// <returns>A task that completes when the invoice is saved.</returns>
    private static async Task SeedInvoiceAsync(ClockedHost host, DateTimeOffset raisedAt)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var baseCurrency = await context.Currencies.SingleAsync(c => c.IsBase, CancellationToken.None);
        var client = Client.Create($"user-{Guid.NewGuid():N}", "Ada", "Lovelace", $"ada-{Guid.NewGuid():N}@example.test", raisedAt);
        context.Clients.Add(client);
        await context.SaveChangesAsync(CancellationToken.None);

        var invoice = Invoice.Create(client.Id, raisedAt.AddDays(14), baseCurrency.Code, raisedAt);
        invoice.Backdate(raisedAt);
        invoice.AddItem("Hosting", 10m, 1);
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/reports/daily-performance");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A client-role caller, not an admin, is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesTheClientRole()
    {
        var client = await ReportsTestSeeds.ClientRoleClientAsync(factory, "daily-performance");

        var response = await client.GetAsync("/api/reports/daily-performance");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>A client for the clocked host carrying the seeded admin's bearer token.</summary>
    /// <param name="host">The host whose clock stands at <see cref="Now"/>.</param>
    /// <returns>An authenticated client.</returns>
    private async Task<HttpClient> AdminClientAsync(ClockedHost host)
    {
        var client = host.CreateClient();
        var token = await factory.GetAdminTokenAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
