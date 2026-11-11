using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Innovayse.Hostpanel.Integration.Tests.Reports;

/// <summary>
/// <c>GET /api/reports/income-by-product-grouped</c> against Postgres: paid-invoice income for
/// the month, grouped by active product group.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetIncomeByProductGroupedTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>With no active product groups the report still answers with an empty, zeroed shape.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AnswersEvenWithNoGroups()
    {
        var client = await ReportsTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/reports/income-by-product-grouped?year=2024&month=1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("month").GetInt32().Should().Be(1);
    }

    /// <summary>
    /// A product's income is what its paid lines cost the payers, by the month they were paid: the
    /// line paid in the last second of January stays in January; in February the base line of 200,
    /// the credit-covered line (no income) and the foreign line (100 in the base) add up to 300.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AttributesPaidLinesToTheirProductByPaidMonth()
    {
        var scenario = await IncomeTestSeeds.SeedAsync(factory, 2035);
        var client = await ReportsTestSeeds.AdminClientAsync(factory);

        var january = await client.GetFromJsonAsync<JsonElement>("/api/reports/income-by-product-grouped?year=2035&month=1");
        var february = await client.GetFromJsonAsync<JsonElement>("/api/reports/income-by-product-grouped?year=2035&month=2");

        var januaryRow = ProductRow(january, scenario.ProductId);
        januaryRow.GetProperty("unitsSold").GetInt32().Should().Be(1);
        januaryRow.GetProperty("totalIncome").GetDecimal().Should().Be(100m);
        var februaryRow = ProductRow(february, scenario.ProductId);
        februaryRow.GetProperty("unitsSold").GetInt32().Should().Be(3);
        februaryRow.GetProperty("totalIncome").GetDecimal().Should().Be(300m);
        february.GetProperty("currency").GetString().Should().Be(scenario.BaseCurrency);
    }

    /// <summary>An out-of-range month is refused before the handler runs.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnInvalidMonth()
    {
        var client = await ReportsTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/reports/income-by-product-grouped?year=2024&month=13");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/reports/income-by-product-grouped?year=2024&month=1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A client-role caller, not an admin, is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesTheClientRole()
    {
        var client = await ReportsTestSeeds.ClientRoleClientAsync(factory, "income-by-product-grouped");

        var response = await client.GetAsync("/api/reports/income-by-product-grouped?year=2024&month=1");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>Finds a product's row in the grouped report.</summary>
    /// <param name="report">The report body.</param>
    /// <param name="productId">The product to find.</param>
    /// <returns>The product's row.</returns>
    private static JsonElement ProductRow(JsonElement report, int productId) =>
        report.GetProperty("groups").EnumerateArray()
            .SelectMany(g => g.GetProperty("products").EnumerateArray())
            .Single(p => p.GetProperty("productId").GetInt32() == productId);
}
