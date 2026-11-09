using System.Net;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>GET /api/billing/{id}</c> against Postgres: reading a single invoice with its line items,
/// and the <c>INVOICE_NOT_FOUND</c> refusal for an id that names no invoice.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetInvoiceTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An admin reads back the invoice they just created, with its client's name.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetByIdReturnsTheCreatedInvoice()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory, firstName: "Grace", lastName: "Hopper");
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 12.50m);

        var response = await admin.GetAsync($"/api/billing/{invoiceId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("id").GetInt32().Should().Be(invoiceId);
        body.RootElement.GetProperty("clientId").GetInt32().Should().Be(clientId);
        body.RootElement.GetProperty("clientName").GetString().Should().Be("Grace Hopper");
    }

    /// <summary>Reading an id that names no invoice answers 404 with the <c>INVOICE_NOT_FOUND</c> code.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetUnknownInvoiceRefusesNotFound()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/billing/{BillingTestSeeds.NoSuchId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.INVOICE_NOT_FOUND);
    }

    /// <summary>Reading an invoice is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetRefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/billing/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
