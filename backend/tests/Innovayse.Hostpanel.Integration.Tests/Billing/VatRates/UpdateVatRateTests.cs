using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Billing.VatRates;

/// <summary>Integration tests for the <c>UpdateVatRate</c> use case: <c>PUT /api/admin/vat-rates/{id}</c>.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpdateVatRateTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Updating replaces the rate and the first day of a row, and the change reads back.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_ReplacesTheFields()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var id = await VatRateTestSeeds.CreateAsync(admin, "IE", "Standard", 23m, new DateOnly(2094, 1, 1));

        var response = await admin.PutAsJsonAsync(
            $"{VatRateTestSeeds.Route}/{id}", VatRateTestSeeds.Body("ie", "Standard", 22.5m, new DateOnly(2094, 2, 1)));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var page = await VatRateTestSeeds.ListAsync(admin, "page=1&pageSize=100&search=ie");
        var row = page.GetProperty("items").EnumerateArray().Single(r => r.GetProperty("id").GetInt32() == id);
        row.GetProperty("ratePercent").GetDecimal().Should().Be(22.5m);
        row.GetProperty("validFrom").GetString().Should().Be("2094-02-01");
    }

    /// <summary>A row that does not exist answers 404 with its code.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_UnknownId_IsNotFound()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.PutAsJsonAsync(
            $"{VatRateTestSeeds.Route}/2147483000", VatRateTestSeeds.Body("IE", "Standard", 23m, new DateOnly(2095, 1, 1)));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.VAT_RATE_NOT_FOUND);
    }

    /// <summary>Moving a row onto the country, kind and day another row holds answers 409.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_OntoAnotherRowsKey_RefusesAsAlreadyExisting()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        await VatRateTestSeeds.CreateAsync(admin, "AT", "Standard", 20m, new DateOnly(2096, 1, 1));
        var id = await VatRateTestSeeds.CreateAsync(admin, "AT", "Standard", 21m, new DateOnly(2096, 6, 1));

        var response = await admin.PutAsJsonAsync(
            $"{VatRateTestSeeds.Route}/{id}", VatRateTestSeeds.Body("AT", "Standard", 21m, new DateOnly(2096, 1, 1)));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.VAT_RATE_ALREADY_EXISTS);
    }

    /// <summary>Saving a row unchanged is not a clash with itself.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_WithTheRowsOwnKey_Succeeds()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var id = await VatRateTestSeeds.CreateAsync(admin, "DK", "Standard", 25m, new DateOnly(2097, 1, 1));

        var response = await admin.PutAsJsonAsync(
            $"{VatRateTestSeeds.Route}/{id}", VatRateTestSeeds.Body("DK", "Standard", 24m, new DateOnly(2097, 1, 1)));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>A country outside the EU is refused with 400.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_RefusesANonMemberState()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var id = await VatRateTestSeeds.CreateAsync(admin, "SE", "Standard", 25m, new DateOnly(2098, 1, 1));

        var response = await admin.PutAsJsonAsync(
            $"{VatRateTestSeeds.Route}/{id}", VatRateTestSeeds.Body("NO", "Standard", 25m, new DateOnly(2098, 1, 1)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
