using System.Net;
using System.Text.Json;
using Innovayse.Hostpanel.Shared.Constants;

namespace Innovayse.Hostpanel.Integration.Tests.Billing.VatRates;

/// <summary>Integration tests for the <c>ListVatRates</c> use case: <c>GET /api/admin/vat-rates</c>, the paged list searched on the country.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListVatRatesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The shipped rates are listed by country, and a country's latest start comes first.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_ReturnsTheShippedRatesOfACountryLatestFirst()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var page = await VatRateTestSeeds.ListAsync(admin, "page=1&pageSize=100&search=fi");

        var rows = page.GetProperty("items").EnumerateArray()
            .Where(r => r.GetProperty("country").GetString() == "FI" && r.GetProperty("kind").GetString() == "Standard")
            .Select(r => r.GetProperty("validFrom").GetString())
            .ToList();
        rows.Should().Contain("2024-09-01").And.Contain("2024-01-01");
        rows.IndexOf("2024-09-01").Should().BeLessThan(rows.IndexOf("2024-01-01"));
    }

    /// <summary>Every member state ships with a standard rate.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_HoldsAStandardRateForEveryMemberState()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var page = await VatRateTestSeeds.ListAsync(admin, "page=1&pageSize=100");

        var countries = page.GetProperty("items").EnumerateArray()
            .Where(r => r.GetProperty("kind").GetString() == "Standard")
            .Select(r => r.GetProperty("country").GetString())
            .Distinct()
            .Count();
        page.GetProperty("totalCount").GetInt32().Should().BeGreaterThanOrEqualTo(27);
        countries.Should().BeGreaterThanOrEqualTo(27);
    }

    /// <summary>The kind filter keeps only the rows of that kind.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_KindFilterKeepsOnlyThatKind()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var page = await VatRateTestSeeds.ListAsync(admin, "page=1&pageSize=100&kind=Standard");

        var kinds = page.GetProperty("items").EnumerateArray().Select(r => r.GetProperty("kind").GetString()).Distinct().ToList();
        kinds.Should().Equal("Standard");
    }

    /// <summary>
    /// The status filter tells the row in force from the one a later row replaced: Finland's standard rate of
    /// 2024-09-01 is the active one and the one of 2024-01-01 is superseded.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_StatusFilterSeparatesTheActiveRowFromTheSupersededOne()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var active = await VatRateTestSeeds.ListAsync(admin, "page=1&pageSize=100&search=fi&kind=Standard&status=Active");
        var superseded = await VatRateTestSeeds.ListAsync(admin, "page=1&pageSize=100&search=fi&kind=Standard&status=Superseded");

        DatesOf(active).Should().Contain("2024-09-01").And.NotContain("2024-01-01");
        DatesOf(superseded).Should().Contain("2024-01-01").And.NotContain("2024-09-01");
    }

    /// <summary>The upcoming filter lists only rows whose first day is still ahead.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_UpcomingFilterKeepsOnlyRowsThatHaveNotStarted()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        // A row of its own, so the filter has something to keep whatever the seeded rates are today; a day years ahead picked at random never meets another run's row.
        var start = DateOnly.FromDateTime(TestMoments.Present().UtcDateTime).AddYears(3).AddDays(Random.Shared.Next(1, 300));
        await VatRateTestSeeds.CreateAsync(admin, "FI", "Standard", 24m, start);

        var page = await VatRateTestSeeds.ListAsync(admin, "page=1&pageSize=100&status=Upcoming");

        var today = DateOnly.FromDateTime(TestMoments.Present().UtcDateTime).ToString("yyyy-MM-dd");
        DatesOf(page).Should().Contain(start.ToString("yyyy-MM-dd")).And.OnlyContain(date => string.CompareOrdinal(date, today) > 0);
    }

    /// <summary>A kind or status that names nothing, by name or by number, is refused with 400 rather than listing everything.</summary>
    /// <param name="filter">The filter's key and value.</param>
    /// <param name="field">The field the refusal must name.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("kind=Nonsense", "kind")]
    [InlineData("kind=9", "kind")]
    [InlineData("status=Nonsense", "status")]
    [InlineData("status=9", "status")]
    public async Task List_UnknownFilterValue_IsRefused(string filter, string field)
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"{VatRateTestSeeds.Route}?page=1&pageSize=20&{filter}");

        await ProductsTestSeeds.AssertFieldRefusedAsync(response, field);
    }

    /// <summary>A page size outside the allowed range is refused with 400.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_RefusesAPageSizeOutOfRange()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"{VatRateTestSeeds.Route}?page=1&pageSize=1000");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync($"{VatRateTestSeeds.Route}?page=1&pageSize=10");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Reads the first days of the rows of a page.</summary>
    /// <param name="page">The list answer.</param>
    /// <returns>Each row's <c>validFrom</c>, <c>yyyy-MM-dd</c>.</returns>
    private static List<string?> DatesOf(JsonElement page)
        => page.GetProperty("items").EnumerateArray().Select(r => r.GetProperty("validFrom").GetString()).ToList();
}
