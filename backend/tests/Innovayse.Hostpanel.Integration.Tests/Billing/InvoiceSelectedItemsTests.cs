using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>POST /api/clients/{clientId}/billable-items/invoice-selected</c> against Postgres: raising
/// an invoice for a set of uninvoiced billable items, and the <c>BILLABLE_ITEM_NOT_FOUND</c>,
/// <c>BILLABLE_ITEM_CLIENT_MISMATCH</c> and <c>BILLABLE_ITEM_INVOICED</c> refusals.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class InvoiceSelectedItemsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Invoicing a selected item creates an invoice and marks the item invoiced.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task InvoiceSelectedCreatesAnInvoiceAndMarksTheItemInvoiced()
    {
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var created = await admin.PostAsJsonAsync($"/api/clients/{clientId}/billable-items", new
        {
            description = "One-off charge",
            amount = 75m,
            currency = "USD",
            type = nameof(BillableItemType.OneTime),
        });
        var itemId = await created.Content.ReadFromJsonAsync<int>();

        var response = await admin.PostAsJsonAsync(
            $"/api/clients/{clientId}/billable-items/invoice-selected", new { billableItemIds = new[] { itemId } });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await admin.GetAsync($"/api/clients/{clientId}/billable-items");
        using var body = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("uninvoicedItems").EnumerateArray()
            .Should().NotContain(item => item.GetProperty("id").GetInt32() == itemId);
    }

    /// <summary>Invoicing an id that names no billable item answers 404 with <c>BILLABLE_ITEM_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task InvoiceSelectedWithUnknownIdRefusesNotFound()
    {
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsJsonAsync(
            $"/api/clients/{clientId}/billable-items/invoice-selected",
            new { billableItemIds = new[] { BillingTestSeeds.NoSuchId } });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.BILLABLE_ITEM_NOT_FOUND);
    }

    /// <summary>
    /// Invoicing a billable item that belongs to a different client answers 400 with
    /// <c>BILLABLE_ITEM_CLIENT_MISMATCH</c>.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task InvoiceSelectedForWrongClientRefusesClientMismatch()
    {
        var ownerId = await BillingTestSeeds.SeedClientAsync(factory);
        var otherId = await BillingTestSeeds.SeedClientAsync(factory);
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var created = await admin.PostAsJsonAsync($"/api/clients/{ownerId}/billable-items", new
        {
            description = "Owned by ownerId",
            amount = 50m,
            currency = "USD",
            type = nameof(BillableItemType.OneTime),
        });
        var itemId = await created.Content.ReadFromJsonAsync<int>();

        var response = await admin.PostAsJsonAsync(
            $"/api/clients/{otherId}/billable-items/invoice-selected",
            new { billableItemIds = new[] { itemId } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.BILLABLE_ITEM_CLIENT_MISMATCH);
    }

    /// <summary>Invoicing an already-invoiced billable item answers 400 with <c>BILLABLE_ITEM_INVOICED</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task InvoiceSelectedTwiceRefusesAlreadyInvoiced()
    {
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var created = await admin.PostAsJsonAsync($"/api/clients/{clientId}/billable-items", new
        {
            description = "One-off charge",
            amount = 75m,
            currency = "USD",
            type = nameof(BillableItemType.OneTime),
        });
        var itemId = await created.Content.ReadFromJsonAsync<int>();

        var firstInvoice = await admin.PostAsJsonAsync(
            $"/api/clients/{clientId}/billable-items/invoice-selected", new { billableItemIds = new[] { itemId } });
        firstInvoice.StatusCode.Should().Be(HttpStatusCode.OK);

        var secondInvoice = await admin.PostAsJsonAsync(
            $"/api/clients/{clientId}/billable-items/invoice-selected", new { billableItemIds = new[] { itemId } });

        secondInvoice.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(secondInvoice)).Should().Be(HostpanelErrorCodes.BILLABLE_ITEM_INVOICED);
    }

    /// <summary>Invoicing selected items is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task InvoiceSelectedRefusesAnonymous()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/clients/1/billable-items/invoice-selected", new { billableItemIds = new[] { 1 } });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
