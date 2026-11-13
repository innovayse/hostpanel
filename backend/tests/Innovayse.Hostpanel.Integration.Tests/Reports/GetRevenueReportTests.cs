using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Innovayse.Hostpanel.Integration.Tests.Reports;

/// <summary>
/// <c>GET /api/admin/reports/revenue</c>: an admin reads the daily revenue items for a
/// date range, a start after the end answers an empty report rather than a refusal, a caller with
/// only the Client role is refused, and the route is closed to a caller with no credential. This
/// use case refuses nothing once authorized.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetRevenueReportTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An admin reads the revenue report for a well-formed range.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminCanReadTheReportAsync()
    {
        var client = await AdminTestSeeds.AdminClientAsync(factory);
        var from = Uri.EscapeDataString(TestMoments.Seeded.AddDays(-30).ToString("O"));
        var to = Uri.EscapeDataString(TestMoments.Seeded.ToString("O"));

        var response = await client.GetAsync($"/api/admin/reports/revenue?startDate={from}&endDate={to}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Each day carries its income in the base currency, and the window is half-open: a payment in
    /// the last second of January lands on 31 January, and a window ending at 1 February leaves out
    /// the payment taken at that very instant.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SumsIncomePerDayInsideAHalfOpenWindowAsync()
    {
        var scenario = await IncomeTestSeeds.SeedAsync(factory, 2033);
        var client = await AdminTestSeeds.AdminClientAsync(factory);
        var start = Uri.EscapeDataString(IncomeTestSeeds.At(2033, 1, 31).ToString("O"));
        var throughFebruaryFirst = Uri.EscapeDataString(IncomeTestSeeds.At(2033, 2, 2).ToString("O"));
        var untilFebruaryFirst = Uri.EscapeDataString(IncomeTestSeeds.At(2033, 2, 1).ToString("O"));

        var both = await client.GetFromJsonAsync<List<JsonElement>>($"/api/admin/reports/revenue?startDate={start}&endDate={throughFebruaryFirst}");
        var januaryOnly = await client.GetFromJsonAsync<List<JsonElement>>($"/api/admin/reports/revenue?startDate={start}&endDate={untilFebruaryFirst}");

        both!.Select(r => (r.GetProperty("date").GetString(), r.GetProperty("revenue").GetDecimal()))
            .Should().Equal(("2033-01-31", 100m), ("2033-02-01", 200m));
        both.Should().OnlyContain(r => r.GetProperty("currency").GetString() == scenario.BaseCurrency);
        januaryOnly!.Select(r => r.GetProperty("date").GetString()).Should().Equal("2033-01-31");
    }

    /// <summary>A start after the end answers an empty report rather than a refusal.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task StartAfterEnd_AnswersAnEmptyReportAsync()
    {
        var client = await AdminTestSeeds.AdminClientAsync(factory);
        var from = Uri.EscapeDataString(TestMoments.Seeded.ToString("O"));
        var to = Uri.EscapeDataString(TestMoments.Seeded.AddDays(-30).ToString("O"));

        var response = await client.GetAsync($"/api/admin/reports/revenue?startDate={from}&endDate={to}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
        items.Should().BeEmpty();
    }

    /// <summary>A caller with only the Client role is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToTheClientRoleAsync()
    {
        var client = await AdminTestSeeds.ClientRoleClientAsync(factory, "revenue-report-forbidden");

        var response = await client.GetAsync("/api/admin/reports/revenue");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>The revenue report is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().GetAsync("/api/admin/reports/revenue");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
