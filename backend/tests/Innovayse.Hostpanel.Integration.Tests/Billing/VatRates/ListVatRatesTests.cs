using System.Net;
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
}
