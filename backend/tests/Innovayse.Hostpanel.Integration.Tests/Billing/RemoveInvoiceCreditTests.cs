using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>POST /api/billing/{id}/credit/remove</c> against Postgres: removing previously-applied
/// credit restores the invoice's total, and the <c>INVOICE_NOT_FOUND</c> refusal for an id that
/// names no invoice.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class RemoveInvoiceCreditTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Removing credit that was applied earlier restores the invoice's total.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RemoveCreditRestoresTheTotal()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        await BillingTestSeeds.GiveCreditAsync(factory, clientId, 5m);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 19.99m);
        (await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/credit", new { amount = 5m }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/credit/remove", new { amount = 5m });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var fetched = await admin.GetAsync($"/api/billing/{invoiceId}");
        using var body = JsonDocument.Parse(await fetched.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("amounts").GetProperty("total").GetDecimal().Should().Be(19.99m);
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(5m);
    }

    /// <summary>Removing credit against an id that names no invoice answers 404 with <c>INVOICE_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RemoveCreditRefusesAnUnknownInvoice()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsJsonAsync($"/api/billing/{BillingTestSeeds.NoSuchId}/credit/remove", new { amount = 5m });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.INVOICE_NOT_FOUND);
    }

    /// <summary>Removing credit is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RemoveCreditRefusesAnonymous()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/billing/1/credit/remove", new { amount = 5m });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Credit cannot be taken off a paid invoice: 400 with <c>INVOICE_NOT_EDITABLE</c>, balance untouched.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RemoveCreditRefusesAPaidInvoice()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        await BillingTestSeeds.GiveCreditAsync(factory, clientId, 5m);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 19.99m);
        (await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/credit", new { amount = 5m }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        await BillingTestSeeds.MarkPaidAsync(admin, invoiceId, 14.99m);

        var response = await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/credit/remove", new { amount = 5m });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.INVOICE_NOT_EDITABLE);
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(0m);
    }
}
