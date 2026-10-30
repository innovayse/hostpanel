using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Innovayse.Hostpanel.Integration.Tests.Reports;

/// <summary>
/// <c>GET /api/reports/income-forecast</c> against Postgres: twelve months of projected income.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetIncomeForecastTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The forecast projects the same twelve months the annual income report shows.</summary>
    [Fact]
    public async Task ReturnsTwelveMonths()
    {
        var client = await ReportsTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/reports/income-forecast?year=2021");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var rows = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
        rows.Should().HaveCount(12);
    }

    /// <summary>
    /// The forecast windows add up the same income the annual report books: payments less refunds,
    /// settled totals, no credit, the foreign invoice converted to the base currency.
    /// </summary>
    [Fact]
    public async Task BuildsTheWindowsFromTheBookedIncome()
    {
        var scenario = await IncomeTestSeeds.SeedAsync(factory, 2032);
        var client = await ReportsTestSeeds.AdminClientAsync(factory);

        var rows = (await client.GetFromJsonAsync<List<JsonElement>>("/api/reports/income-forecast?year=2032"))!;

        rows.Select(r => r.GetProperty("monthly").GetDecimal())
            .Should().Equal(100m, 300m, -50m, 40m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m);
        rows[0].GetProperty("quarterly").GetDecimal().Should().Be(100m);
        rows[1].GetProperty("quarterly").GetDecimal().Should().Be(400m);
        rows[2].GetProperty("quarterly").GetDecimal().Should().Be(350m);
        rows[0].GetProperty("semiAnnual").GetDecimal().Should().Be(100m);
        rows[3].GetProperty("total").GetDecimal().Should().Be(390m);
        rows[0].GetProperty("annual").GetDecimal().Should().Be(390m);
        rows.Should().OnlyContain(r => r.GetProperty("currency").GetString() == scenario.BaseCurrency);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    [Fact]
    public async Task RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/reports/income-forecast?year=2021");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A client-role caller, not an admin, is refused with 403.</summary>
    [Fact]
    public async Task RefusesTheClientRole()
    {
        var client = await ReportsTestSeeds.ClientRoleClientAsync(factory, "income-forecast");

        var response = await client.GetAsync("/api/reports/income-forecast?year=2021");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
