using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>POST /api/billing/{id}/credit</c> against Postgres: applying credit moves it from the
/// client's balance onto the invoice, never more than either holds, and the
/// <c>INVOICE_NOT_FOUND</c> refusal for an id that names no invoice.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ApplyInvoiceCreditTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Applying credit reduces the invoice's total owed and the client's balance by the same amount.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ApplyCreditReducesTheTotal()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        await BillingTestSeeds.GiveCreditAsync(factory, clientId, 8m);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 19.99m);

        var response = await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/credit", new { amount = 5m });
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var fetched = await admin.GetAsync($"/api/billing/{invoiceId}");
        using var body = JsonDocument.Parse(await fetched.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("amounts").GetProperty("total").GetDecimal().Should().Be(14.99m);
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(3m);
    }

    /// <summary>Applying more credit than the client holds answers 400 with <c>CLIENT_CREDIT_INSUFFICIENT</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ApplyCreditRefusesMoreThanTheBalance()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        await BillingTestSeeds.GiveCreditAsync(factory, clientId, 2m);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 19.99m);

        var response = await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/credit", new { amount = 5m });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.CLIENT_CREDIT_INSUFFICIENT);
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(2m);
    }

    /// <summary>Applying more credit than the invoice owes answers 400 with <c>INVOICE_CREDIT_EXCEEDS_DUE</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ApplyCreditRefusesMoreThanTheInvoiceOwes()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        await BillingTestSeeds.GiveCreditAsync(factory, clientId, 50m);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 10m);

        var response = await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/credit", new { amount = 11m });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.INVOICE_CREDIT_EXCEEDS_DUE);
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(50m);
    }

    /// <summary>Applying credit against an id that names no invoice answers 404 with <c>INVOICE_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ApplyCreditRefusesAnUnknownInvoice()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsJsonAsync($"/api/billing/{BillingTestSeeds.NoSuchId}/credit", new { amount = 5m });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.INVOICE_NOT_FOUND);
    }

    /// <summary>Applying credit is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ApplyCreditRefusesAnonymous()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/billing/1/credit", new { amount = 5m });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Credit that covers what a partial payment left settles the invoice in the same save: nothing
    /// is owed, so it does not stay open and go overdue.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ApplyCreditCoveringTheRestOfAPartPaidInvoiceSettlesIt()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        await BillingTestSeeds.GiveCreditAsync(factory, clientId, 6m);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 10m);
        await BillingTestSeeds.MarkPaidAsync(admin, invoiceId, 4m);

        var response = await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/credit", new { amount = 6m });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var body = JsonDocument.Parse(await (await admin.GetAsync($"/api/billing/{invoiceId}")).Content.ReadAsStringAsync());
        body.RootElement.GetProperty("status").GetString().Should().Be(nameof(InvoiceStatus.Paid));
        body.RootElement.GetProperty("amountDue").GetDecimal().Should().Be(0m);
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(0m);
    }
}
