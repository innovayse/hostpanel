using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Billing;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>PUT /api/billing/{id}/options</c> against Postgres: writing an invoice's dates, payment
/// method and tax rate, and the <c>INVOICE_NOT_FOUND</c> refusal for an id that names no invoice.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpdateInvoiceOptionsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Writing a tax rate recomputes the invoice's total.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateOptionsRecomputesTheTotalFromTheTaxRate()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 100m);

        var response = await admin.PutAsJsonAsync($"/api/billing/{invoiceId}/options", new
        {
            invoiceDate = DateTimeOffset.UtcNow,
            dueDate = DateTimeOffset.UtcNow.AddDays(7),
            paymentMethod = PaymentMethodNames.Manual,
            taxRate = 10m,
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var fetched = await admin.GetAsync($"/api/billing/{invoiceId}");
        using var body = JsonDocument.Parse(await fetched.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("amounts").GetProperty("total").GetDecimal().Should().Be(110m);
        body.RootElement.GetProperty("payment").GetProperty("method").GetString().Should().Be(PaymentMethodNames.Manual);
    }

    /// <summary>The invoice date and the due date are stored and read back as written.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateOptionsStoresTheDates()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 100m);
        var invoiceDate = new DateTimeOffset(2030, 3, 1, 0, 0, 0, TimeSpan.Zero);
        var dueDate = new DateTimeOffset(2030, 3, 31, 0, 0, 0, TimeSpan.Zero);

        var response = await admin.PutAsJsonAsync($"/api/billing/{invoiceId}/options", new
        {
            invoiceDate,
            dueDate,
            paymentMethod = PaymentMethodNames.Manual,
            taxRate = 0m,
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var fetched = await admin.GetAsync($"/api/billing/{invoiceId}");
        using var body = JsonDocument.Parse(await fetched.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("dates").GetProperty("invoiceDate").GetDateTimeOffset().Should().Be(invoiceDate);
        body.RootElement.GetProperty("dates").GetProperty("dueDate").GetDateTimeOffset().Should().Be(dueDate);
    }

    /// <summary>A tax rate above 100 is refused as a validation error and the stored total is kept.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateOptionsRefusesATaxRateAboveOneHundred()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 100m);

        var response = await admin.PutAsJsonAsync($"/api/billing/{invoiceId}/options", new
        {
            invoiceDate = DateTimeOffset.UtcNow,
            dueDate = DateTimeOffset.UtcNow.AddDays(7),
            paymentMethod = PaymentMethodNames.Manual,
            taxRate = 101m,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var fetched = await admin.GetAsync($"/api/billing/{invoiceId}");
        using var body = JsonDocument.Parse(await fetched.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("amounts").GetProperty("total").GetDecimal().Should().Be(100m);
    }

    /// <summary>Updating options on an id that names no invoice answers 404, not 400.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateOptionsOnUnknownInvoiceRefusesNotFound()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.PutAsJsonAsync($"/api/billing/{BillingTestSeeds.NoSuchId}/options", new
        {
            invoiceDate = DateTimeOffset.UtcNow,
            dueDate = DateTimeOffset.UtcNow.AddDays(7),
            paymentMethod = (string?)null,
            taxRate = 0m,
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.INVOICE_NOT_FOUND);
    }

    /// <summary>Updating options is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateOptionsRefusesAnonymous()
    {
        var response = await factory.CreateClient().PutAsJsonAsync("/api/billing/1/options", new
        {
            invoiceDate = DateTimeOffset.UtcNow,
            dueDate = DateTimeOffset.UtcNow.AddDays(7),
            paymentMethod = (string?)null,
            taxRate = 0m,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>The tax rate of a paid invoice cannot change: 400 with <c>INVOICE_NOT_EDITABLE</c>, total unchanged.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateOptionsRefusesATaxRateChangeOnAPaidInvoice()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 100m);
        await BillingTestSeeds.MarkPaidAsync(admin, invoiceId, 100m);

        var response = await admin.PutAsJsonAsync($"/api/billing/{invoiceId}/options", new
        {
            invoiceDate = DateTimeOffset.UtcNow,
            dueDate = DateTimeOffset.UtcNow.AddDays(7),
            paymentMethod = PaymentMethodNames.Manual,
            taxRate = 10m,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.INVOICE_NOT_EDITABLE);
        var fetched = await admin.GetAsync($"/api/billing/{invoiceId}");
        using var body = JsonDocument.Parse(await fetched.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("amounts").GetProperty("total").GetDecimal().Should().Be(100m);
    }

    /// <summary>
    /// Lowering the tax rate below what the applied credit covers sends the surplus credit back to
    /// the client's balance in the same save.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateOptionsReturnsCreditTheLowerTaxNoLongerNeeds()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        await BillingTestSeeds.GiveCreditAsync(factory, clientId, 110m);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 100m);
        (await PutTaxRateAsync(admin, invoiceId, 10m)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/credit", new { amount = 110m }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await PutTaxRateAsync(admin, invoiceId, 0m);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var fetched = await admin.GetAsync($"/api/billing/{invoiceId}");
        using var body = JsonDocument.Parse(await fetched.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("amounts").GetProperty("total").GetDecimal().Should().Be(0m);
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(10m);
    }

    /// <summary>
    /// A tax cut that would make the invoice charge less than the payments it already took is
    /// refused with <c>INVOICE_TOTAL_BELOW_PAID</c> and the total is kept.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateOptionsRefusesATaxCutBelowThePaymentsTaken()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 100m);
        (await PutTaxRateAsync(admin, invoiceId, 10m)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        await BillingTestSeeds.MarkPaidAsync(admin, invoiceId, 105m);

        var response = await PutTaxRateAsync(admin, invoiceId, 0m);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.INVOICE_TOTAL_BELOW_PAID);
        var fetched = await admin.GetAsync($"/api/billing/{invoiceId}");
        using var body = JsonDocument.Parse(await fetched.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("amounts").GetProperty("total").GetDecimal().Should().Be(110m);
    }

    /// <summary>Writes only a new tax rate on an invoice, keeping fixed dates and the manual method.</summary>
    /// <param name="admin">Signed-in admin client.</param>
    /// <param name="invoiceId">The invoice to change.</param>
    /// <param name="taxRate">The tax rate to write.</param>
    /// <returns>The API's answer.</returns>
    private static Task<HttpResponseMessage> PutTaxRateAsync(HttpClient admin, int invoiceId, decimal taxRate) =>
        admin.PutAsJsonAsync($"/api/billing/{invoiceId}/options", new
        {
            invoiceDate = new DateTimeOffset(2030, 3, 1, 0, 0, 0, TimeSpan.Zero),
            dueDate = new DateTimeOffset(2030, 3, 31, 0, 0, 0, TimeSpan.Zero),
            paymentMethod = PaymentMethodNames.Manual,
            taxRate,
        });

    /// <summary>A tax rate is refused on the invoice of a tax-exempt client and the invoice is left as it was.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateOptionsRefusesATaxRateForATaxExemptClient()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 100m);
        await ClientSettingsSeeds.SetAsync(factory, clientId, taxExempt: true);

        var response = await admin.PutAsJsonAsync($"/api/billing/{invoiceId}/options", new
        {
            invoiceDate = DateTimeOffset.UtcNow,
            dueDate = DateTimeOffset.UtcNow.AddDays(7),
            paymentMethod = PaymentMethodNames.Manual,
            taxRate = 10m,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_TAX_EXEMPT);
        var fetched = await admin.GetAsync($"/api/billing/{invoiceId}");
        using var body = JsonDocument.Parse(await fetched.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("amounts").GetProperty("total").GetDecimal().Should().Be(100m);
    }

    /// <summary>A tax-exempt client's invoice can still be saved with no tax, so its dates and method stay editable.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateOptionsAllowsNoTaxForATaxExemptClient()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 100m);
        await ClientSettingsSeeds.SetAsync(factory, clientId, taxExempt: true);

        var response = await admin.PutAsJsonAsync($"/api/billing/{invoiceId}/options", new
        {
            invoiceDate = DateTimeOffset.UtcNow,
            dueDate = DateTimeOffset.UtcNow.AddDays(7),
            paymentMethod = PaymentMethodNames.Manual,
            taxRate = 0m,
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
