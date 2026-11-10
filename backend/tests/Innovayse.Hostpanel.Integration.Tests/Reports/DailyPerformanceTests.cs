using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

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
