using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Innovayse.Hostpanel.Integration.Tests.Reports;

/// <summary>
/// <c>GET /api/reports/top-clients-by-income</c> against Postgres: the top clients by
/// net invoice income, highest first, in the base currency.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class TopClientsByIncomeTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// Clients are ranked by net invoice income converted to the base currency: the base client's
    /// 340 taken less 50 refunded comes before the foreign client's 40,000 (100 in the base), and its
    /// 1,000 credit top-up is not income.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RanksClientsByTheirConvertedInvoiceIncome()
    {
        var scenario = await IncomeTestSeeds.SeedAsync(factory, 2034);
        var client = await ReportsTestSeeds.AdminClientAsync(factory);

        var rows = await client.GetFromJsonAsync<List<JsonElement>>("/api/reports/top-clients-by-income?take=2");

        rows.Should().HaveCount(2);
        rows![0].GetProperty("clientId").GetInt32().Should().Be(scenario.BaseClientId);
        rows[0].GetProperty("clientName").GetString().Should().Be("Base Payer");
        rows[0].GetProperty("totalAmountIn").GetDecimal().Should().Be(340m);
        rows[0].GetProperty("totalAmountOut").GetDecimal().Should().Be(50m);
        rows[0].GetProperty("balance").GetDecimal().Should().Be(290m);
        rows[1].GetProperty("clientId").GetInt32().Should().Be(scenario.ForeignClientId);
        rows[1].GetProperty("balance").GetDecimal().Should().Be(100m);
        rows.Should().OnlyContain(r => r.GetProperty("currency").GetString() == scenario.BaseCurrency);
    }

    /// <summary>A <c>take</c> outside the allowed range is refused rather than reaching the database's <c>LIMIT</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnInvalidTake()
    {
        var client = await ReportsTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/reports/top-clients-by-income?take=0");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/reports/top-clients-by-income");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A client-role caller, not an admin, is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesTheClientRole()
    {
        var client = await ReportsTestSeeds.ClientRoleClientAsync(factory, "top-clients-by-income");

        var response = await client.GetAsync("/api/reports/top-clients-by-income");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
