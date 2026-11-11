using System.Net;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>POST /api/billing/{id}/publish</c> against Postgres: a draft invoice becomes payable, and
/// the <c>INVOICE_NOT_FOUND</c> refusal for an id that names no invoice.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class PublishInvoiceTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Publishing a draft invoice transitions it to Unpaid.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PublishTransitionsADraftToUnpaid()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 10m, isDraft: true);

        var response = await admin.PostAsync($"/api/billing/{invoiceId}/publish", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var fetched = await admin.GetAsync($"/api/billing/{invoiceId}");
        using var body = JsonDocument.Parse(await fetched.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("status").GetString().Should().Be(nameof(InvoiceStatus.Unpaid));
    }

    /// <summary>Publishing an invoice that is not a draft is refused with a 400.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PublishingAnAlreadyPublishedInvoiceRefusesBadRequest()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 10m);

        var response = await admin.PostAsync($"/api/billing/{invoiceId}/publish", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>Publishing an id that names no invoice answers 404 with <c>INVOICE_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PublishUnknownInvoiceRefusesNotFound()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsync($"/api/billing/{BillingTestSeeds.NoSuchId}/publish", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.INVOICE_NOT_FOUND);
    }

    /// <summary>Publishing an invoice is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PublishRefusesAnonymous()
    {
        var response = await factory.CreateClient().PostAsync("/api/billing/1/publish", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
