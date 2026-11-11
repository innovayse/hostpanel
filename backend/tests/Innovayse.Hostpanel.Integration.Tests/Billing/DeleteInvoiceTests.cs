using System.Net;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>DELETE /api/billing/{id}</c> against Postgres: permanently removing a Draft or Cancelled
/// invoice, the <c>INVOICE_NOT_DELETABLE</c> refusal for any other, and the
/// <c>INVOICE_NOT_FOUND</c> refusal for an id that names no invoice.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class DeleteInvoiceTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Deleting a draft invoice removes it: a later read answers 404.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DeleteRemovesTheInvoice()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 10m, isDraft: true);

        var response = await admin.DeleteAsync($"/api/billing/{invoiceId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var fetched = await admin.GetAsync($"/api/billing/{invoiceId}");
        fetched.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>A cancelled invoice may be deleted.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DeleteRemovesACancelledInvoice()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 10m);
        (await admin.PostAsync($"/api/billing/{invoiceId}/cancel", content: null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await admin.DeleteAsync($"/api/billing/{invoiceId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>Deleting an unpaid invoice answers 400 with <c>INVOICE_NOT_DELETABLE</c> and keeps it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DeleteRefusesAnIssuedInvoice()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 10m);

        var response = await admin.DeleteAsync($"/api/billing/{invoiceId}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.INVOICE_NOT_DELETABLE);
        (await admin.GetAsync($"/api/billing/{invoiceId}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>Deleting an id that names no invoice answers 404 with <c>INVOICE_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DeleteUnknownInvoiceRefusesNotFound()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.DeleteAsync($"/api/billing/{BillingTestSeeds.NoSuchId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.INVOICE_NOT_FOUND);
    }

    /// <summary>
    /// A billable item that was put on the deleted invoice is released: it is no longer invoiced and
    /// the next invoicing run bills it on a new invoice.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DeleteReleasesTheBillableItemsPutOnTheInvoice()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var seeded = await BillableItemSeeds.SeedItemOnACancelledInvoiceAsync(factory);

        var response = await admin.DeleteAsync($"/api/billing/{seeded.InvoiceId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var released = await BillableItemSeeds.ReadItemAsync(factory, seeded.ItemId);
        released.IsInvoiced.Should().BeFalse();
        released.InvoiceId.Should().BeNull();

        await BillableItemSeeds.RunInvoicingAsync(factory);

        var billedAgain = await BillableItemSeeds.ReadItemAsync(factory, seeded.ItemId);
        billedAgain.IsInvoiced.Should().BeTrue();
        billedAgain.InvoiceId.Should().NotBeNull().And.NotBe(seeded.InvoiceId);
    }

    /// <summary>Deleting an invoice is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DeleteRefusesAnonymous()
    {
        var response = await factory.CreateClient().DeleteAsync("/api/billing/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
