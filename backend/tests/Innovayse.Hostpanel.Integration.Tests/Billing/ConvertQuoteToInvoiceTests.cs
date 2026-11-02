using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>POST /api/billing/quotes/{id}/convert-to-invoice</c> against Postgres: converting a quote to
/// a draft invoice, the refusal to convert an already-accepted quote a second time, and the
/// <c>QUOTE_NOT_FOUND</c> refusal for an id that names no quote.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ConvertQuoteToInvoiceTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// Converting an accepted quote a second time answers the aggregate's own stage invariant,
    /// which still throws rather than becoming a refusal code (a business rule, not a lookup), and
    /// the exception middleware turns that into a 400.
    /// </summary>
    [Fact]
    public async Task ConvertingAnAlreadyAcceptedQuoteRefusesBadRequest()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var quoteId = await BillingTestSeeds.CreateQuoteAsync(admin, clientId, 10m);

        var firstConvert = await admin.PostAsync($"/api/billing/quotes/{quoteId}/convert-to-invoice", null);
        firstConvert.StatusCode.Should().Be(HttpStatusCode.Created);

        var secondConvert = await admin.PostAsync($"/api/billing/quotes/{quoteId}/convert-to-invoice", null);
        secondConvert.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>A quote without discounts converts to an invoice with the same total, rounded once after the sum.</summary>
    [Fact]
    public async Task ConvertingAQuoteWithoutDiscountsGivesAnInvoiceWithTheSameTotal()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var created = await admin.PostAsJsonAsync("/api/billing/quotes", new
        {
            clientId,
            subject = "Fractions",
            expiryDate = DateTimeOffset.UtcNow.AddDays(30),
            notes = (string?)null,
            items = new[]
            {
                new { description = "A", unitPrice = 0.004m, quantity = 1 },
                new { description = "B", unitPrice = 0.004m, quantity = 1 },
            },
        });
        var quoteId = await created.Content.ReadFromJsonAsync<int>();
        using var quote = JsonDocument.Parse(await admin.GetStringAsync($"/api/billing/quotes/{quoteId}"));

        var converted = await admin.PostAsync($"/api/billing/quotes/{quoteId}/convert-to-invoice", null);

        converted.StatusCode.Should().Be(HttpStatusCode.Created);
        var invoiceId = await converted.Content.ReadFromJsonAsync<int>();
        using var invoice = JsonDocument.Parse(await admin.GetStringAsync($"/api/billing/{invoiceId}"));
        var quoteTotal = quote.RootElement.GetProperty("total").GetDecimal();
        quoteTotal.Should().Be(0.01m);
        invoice.RootElement.GetProperty("amounts").GetProperty("total").GetDecimal().Should().Be(quoteTotal);
    }

    /// <summary>Converting an id that names no quote answers 404 with <c>QUOTE_NOT_FOUND</c>.</summary>
    [Fact]
    public async Task ConvertUnknownQuoteRefusesNotFound()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsync($"/api/billing/quotes/{BillingTestSeeds.NoSuchId}/convert-to-invoice", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.QUOTE_NOT_FOUND);
    }

    /// <summary>Converting a quote is not for anonymous callers.</summary>
    [Fact]
    public async Task ConvertRefusesAnonymous()
    {
        var response = await factory.CreateClient().PostAsync("/api/billing/quotes/1/convert-to-invoice", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
