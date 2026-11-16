using System.Net;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Billing.VatRates;

/// <summary>Integration tests for the <c>DeleteVatRate</c> use case: <c>DELETE /api/admin/vat-rates/{id}</c>.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class DeleteVatRateTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Deleting a row answers 204 and the row is gone from the list.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Delete_RemovesTheRow()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var id = await VatRateTestSeeds.CreateAsync(admin, "CZ", "Standard", 21m, new DateOnly(2099, 1, 1));

        var response = await admin.DeleteAsync($"{VatRateTestSeeds.Route}/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var page = await VatRateTestSeeds.ListAsync(admin, "page=1&pageSize=100&search=cz");
        page.GetProperty("items").EnumerateArray().Should().NotContain(r => r.GetProperty("id").GetInt32() == id);
    }

    /// <summary>A row that does not exist answers 404 with its code.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Delete_UnknownId_IsNotFound()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.DeleteAsync($"{VatRateTestSeeds.Route}/2147483000");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.VAT_RATE_NOT_FOUND);
    }
}
