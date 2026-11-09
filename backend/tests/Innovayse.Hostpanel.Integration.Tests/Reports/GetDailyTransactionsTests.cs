using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Innovayse.Hostpanel.Integration.Tests.Reports;

/// <summary>
/// <c>GET /api/reports/daily-transactions</c> against Postgres: one row per day of the month,
/// zero-filled where nothing was recorded.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetDailyTransactionsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Every day of the month is present, zero-filled where nothing happened.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ReturnsEveryDayOfTheMonth()
    {
        var client = await ReportsTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/reports/daily-transactions?year=2023&month=2");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("rows").GetArrayLength().Should().Be(28);
    }

    /// <summary>An out-of-range month is refused before the handler runs.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnInvalidMonth()
    {
        var client = await ReportsTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/reports/daily-transactions?year=2024&month=0");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Ledger rows in different currencies are converted to the base before they are added: the
    /// foreign 40,000 on 10 February counts 100, and the month's money in is 200 + 100 + the 1,000 top-up.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ConvertsEveryCurrencyToTheBase()
    {
        var scenario = await IncomeTestSeeds.SeedAsync(factory, 2037);
        var client = await ReportsTestSeeds.AdminClientAsync(factory);

        var february = await client.GetFromJsonAsync<JsonElement>("/api/reports/daily-transactions?year=2037&month=2");
        var march = await client.GetFromJsonAsync<JsonElement>("/api/reports/daily-transactions?year=2037&month=3");

        february.GetProperty("totalAmountIn").GetDecimal().Should().Be(1_300m);
        february.GetProperty("rows").EnumerateArray()
            .Single(r => r.GetProperty("date").GetString() == "2037-02-10")
            .GetProperty("amountIn").GetDecimal().Should().Be(100m);
        february.GetProperty("currency").GetString().Should().Be(scenario.BaseCurrency);
        march.GetProperty("totalAmountOut").GetDecimal().Should().Be(50m);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/reports/daily-transactions?year=2023&month=2");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A client-role caller, not an admin, is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesTheClientRole()
    {
        var client = await ReportsTestSeeds.ClientRoleClientAsync(factory, "daily-transactions");

        var response = await client.GetAsync("/api/reports/daily-transactions?year=2023&month=2");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
