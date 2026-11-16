using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Billing.VatRates;

/// <summary>Integration tests for the <c>CreateVatRate</c> use case: <c>POST /api/admin/vat-rates</c>.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CreateVatRateTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Creating a row answers 201 with its id, and the row reads back with an upper-case country.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_StoresTheRow()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var validFrom = new DateOnly(2090, 1, 1);

        var response = await admin.PostAsJsonAsync(VatRateTestSeeds.Route, VatRateTestSeeds.Body("be", "Reduced", 6m, validFrom));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = await response.Content.ReadFromJsonAsync<int>();
        var page = await VatRateTestSeeds.ListAsync(admin, "page=1&pageSize=100&search=be");
        var row = page.GetProperty("items").EnumerateArray().Single(r => r.GetProperty("id").GetInt32() == id);
        row.GetProperty("country").GetString().Should().Be("BE");
        row.GetProperty("kind").GetString().Should().Be("Reduced");
        row.GetProperty("ratePercent").GetDecimal().Should().Be(6m);
        row.GetProperty("validFrom").GetString().Should().Be("2090-01-01");
    }

    /// <summary>A second row for the same country, kind and first day is refused with 409; another day or kind is allowed.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_SameKeyTwice_RefusesAsAlreadyExisting()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var validFrom = new DateOnly(2091, 1, 1);
        await VatRateTestSeeds.CreateAsync(admin, "NL", "Standard", 21m, validFrom);

        var same = await admin.PostAsJsonAsync(VatRateTestSeeds.Route, VatRateTestSeeds.Body("nl", "Standard", 22m, validFrom));
        var otherDay = await admin.PostAsJsonAsync(VatRateTestSeeds.Route, VatRateTestSeeds.Body("NL", "Standard", 22m, validFrom.AddDays(1)));
        var otherKind = await admin.PostAsJsonAsync(VatRateTestSeeds.Route, VatRateTestSeeds.Body("NL", "Reduced", 9m, validFrom));

        same.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await BillingTestSeeds.CodeOfAsync(same)).Should().Be(HostpanelErrorCodes.VAT_RATE_ALREADY_EXISTS);
        otherDay.StatusCode.Should().Be(HttpStatusCode.Created);
        otherKind.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    /// <summary>A country that is not an EU member state, a kind that is not one, a rate out of range and a start day out of range answer 400.</summary>
    /// <param name="country">The country to send.</param>
    /// <param name="kind">The kind to send.</param>
    /// <param name="ratePercent">The rate to send.</param>
    /// <param name="year">The year of the first day to send.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("US", "Standard", 19, 2092)]
    [InlineData("D", "Standard", 19, 2092)]
    [InlineData("DE", "Super", 19, 2092)]
    [InlineData("DE", "Standard", -1, 2092)]
    [InlineData("DE", "Standard", 100.5, 2092)]
    [InlineData("DE", "Standard", 19.0005, 2092)]
    [InlineData("DE", "Standard", 19, 1900)]
    public async Task Create_RefusesAnInvalidField(string country, string kind, double ratePercent, int year)
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsJsonAsync(
            VatRateTestSeeds.Route, VatRateTestSeeds.Body(country, kind, (decimal)ratePercent, new DateOnly(year, 1, 1)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_RefusesAnonymous()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            VatRateTestSeeds.Route, VatRateTestSeeds.Body("DE", "Standard", 19m, new DateOnly(2093, 1, 1)));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A client, who is not staff, is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_RefusesAClient()
    {
        var (client, _) = await BillingTestSeeds.RegisterClientAsync(factory, null, "create-vat-rate-forbidden");

        var response = await client.PostAsJsonAsync(
            VatRateTestSeeds.Route, VatRateTestSeeds.Body("DE", "Standard", 19m, new DateOnly(2093, 1, 1)));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
