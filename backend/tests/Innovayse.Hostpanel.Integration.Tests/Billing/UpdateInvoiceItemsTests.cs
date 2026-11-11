using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>PUT /api/billing/{id}/items</c> against Postgres: adding, editing and removing line items
/// on an invoice, and the <c>INVOICE_NOT_FOUND</c> refusal for an id that names no invoice.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpdateInvoiceItemsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Replacing an invoice's single line item rewrites its total.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateItemsRewritesTheTotal()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 10m);
        var fetched = await admin.GetAsync($"/api/billing/{invoiceId}");
        using var fetchedBody = JsonDocument.Parse(await fetched.Content.ReadAsStringAsync());
        var itemId = fetchedBody.RootElement.GetProperty("items")[0].GetProperty("id").GetInt32();

        var response = await admin.PutAsJsonAsync($"/api/billing/{invoiceId}/items", new
        {
            items = new[] { new { id = itemId, description = "Hosting", unitPrice = 25m, quantity = 1, isDeleted = false } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var reFetched = await admin.GetAsync($"/api/billing/{invoiceId}");
        using var reFetchedBody = JsonDocument.Parse(await reFetched.Content.ReadAsStringAsync());
        reFetchedBody.RootElement.GetProperty("amounts").GetProperty("subTotal").GetDecimal().Should().Be(25m);
    }

    /// <summary>
    /// One call can add a line, change the quantity of another and remove a third; every change is
    /// stored and the totals are recomputed from what is left.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateItemsAddsEditsAndRemovesLinesInOneCall()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 10m);
        var firstId = await FirstItemIdAsync(admin, invoiceId);
        var seeded = await admin.PutAsJsonAsync($"/api/billing/{invoiceId}/items", new
        {
            items = new[] { new { id = (int?)null, description = "Second", unitPrice = 5m, quantity = 1, isDeleted = false } },
        });
        seeded.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var secondId = (await ItemsOfAsync(admin, invoiceId))
            .Single(i => i.GetProperty("description").GetString() == "Second")
            .GetProperty("id").GetInt32();

        var response = await admin.PutAsJsonAsync($"/api/billing/{invoiceId}/items", new
        {
            items = new[]
            {
                new { id = (int?)firstId, description = "First", unitPrice = 10m, quantity = 3, isDeleted = false },
                new { id = (int?)secondId, description = "Second", unitPrice = 5m, quantity = 1, isDeleted = true },
                new { id = (int?)null, description = "Third", unitPrice = 2m, quantity = 2, isDeleted = false },
            },
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var items = await ItemsOfAsync(admin, invoiceId);
        items.Select(i => i.GetProperty("description").GetString()).Should().BeEquivalentTo("First", "Third");
        items.Single(i => i.GetProperty("description").GetString() == "First")
            .GetProperty("quantity").GetInt32().Should().Be(3);
        var reFetched = await admin.GetAsync($"/api/billing/{invoiceId}");
        using var body = JsonDocument.Parse(await reFetched.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("amounts").GetProperty("subTotal").GetDecimal().Should().Be(34m);
    }

    /// <summary>A line with an empty description is refused as a validation error and nothing is stored.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateItemsRefusesAnEmptyDescription()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 10m);
        var itemId = await FirstItemIdAsync(admin, invoiceId);

        var response = await admin.PutAsJsonAsync($"/api/billing/{invoiceId}/items", new
        {
            items = new[] { new { id = (int?)itemId, description = "", unitPrice = 99m, quantity = 1, isDeleted = false } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var reFetched = await admin.GetAsync($"/api/billing/{invoiceId}");
        using var body = JsonDocument.Parse(await reFetched.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("amounts").GetProperty("subTotal").GetDecimal().Should().Be(10m);
    }

    /// <summary>Reads the line items of an invoice from <c>GET /api/billing/{id}</c>.</summary>
    /// <param name="admin">Signed-in admin client.</param>
    /// <param name="invoiceId">The invoice to read.</param>
    /// <returns>The line item elements, detached from the parsed document.</returns>
    private static async Task<List<JsonElement>> ItemsOfAsync(HttpClient admin, int invoiceId)
    {
        var fetched = await admin.GetAsync($"/api/billing/{invoiceId}");
        using var body = JsonDocument.Parse(await fetched.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("items").EnumerateArray().Select(i => i.Clone()).ToList();
    }

    /// <summary>Reads the id of the first line item of an invoice.</summary>
    /// <param name="admin">Signed-in admin client.</param>
    /// <param name="invoiceId">The invoice to read.</param>
    /// <returns>The first line's id.</returns>
    private static async Task<int> FirstItemIdAsync(HttpClient admin, int invoiceId)
    {
        return (await ItemsOfAsync(admin, invoiceId))[0].GetProperty("id").GetInt32();
    }

    /// <summary>Updating line items on an id that names no invoice answers 404, not 400.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateItemsOnUnknownInvoiceRefusesNotFound()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.PutAsJsonAsync($"/api/billing/{BillingTestSeeds.NoSuchId}/items", new
        {
            items = new[] { new { id = (int?)null, description = "Line", unitPrice = 1m, quantity = 1, isDeleted = false } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.INVOICE_NOT_FOUND);
    }

    /// <summary>Updating line items is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateItemsRefusesAnonymous()
    {
        var response = await factory.CreateClient().PutAsJsonAsync("/api/billing/1/items", new
        {
            items = Array.Empty<object>(),
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>The lines of a paid invoice cannot change: 400 with <c>INVOICE_NOT_EDITABLE</c>, total unchanged.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateItemsRefusesAPaidInvoice()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 10m);
        await BillingTestSeeds.MarkPaidAsync(admin, invoiceId, 10m);
        var fetched = await admin.GetAsync($"/api/billing/{invoiceId}");
        using var fetchedBody = JsonDocument.Parse(await fetched.Content.ReadAsStringAsync());
        var itemId = fetchedBody.RootElement.GetProperty("items")[0].GetProperty("id").GetInt32();

        var response = await admin.PutAsJsonAsync($"/api/billing/{invoiceId}/items", new
        {
            items = new[] { new { id = itemId, description = "Hosting", unitPrice = 1m, quantity = 1, isDeleted = false } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.INVOICE_NOT_EDITABLE);
        var reFetched = await admin.GetAsync($"/api/billing/{invoiceId}");
        using var reFetchedBody = JsonDocument.Parse(await reFetched.Content.ReadAsStringAsync());
        reFetchedBody.RootElement.GetProperty("amounts").GetProperty("total").GetDecimal().Should().Be(10m);
    }

    /// <summary>
    /// Lowering a line below the credit applied to the invoice sends the credit it no longer needs
    /// back to the client's balance in the same save, so the total never goes negative.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateItemsReturnsCreditTheLowerTotalNoLongerNeeds()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        await BillingTestSeeds.GiveCreditAsync(factory, clientId, 10m);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 10m);
        (await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/credit", new { amount = 8m }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        var itemId = await FirstItemIdAsync(admin, invoiceId);

        var response = await admin.PutAsJsonAsync($"/api/billing/{invoiceId}/items", new
        {
            items = new[] { new { id = (int?)itemId, description = "Hosting", unitPrice = 5m, quantity = 1, isDeleted = false } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var reFetched = await admin.GetAsync($"/api/billing/{invoiceId}");
        using var body = JsonDocument.Parse(await reFetched.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("amounts").GetProperty("total").GetDecimal().Should().Be(0m);
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(5m);
    }

    /// <summary>
    /// A line change that would make the invoice charge less than the payments it already took is
    /// refused with <c>INVOICE_TOTAL_BELOW_PAID</c> and nothing is stored.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateItemsRefusesATotalBelowThePaymentsTaken()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 10m);
        await BillingTestSeeds.MarkPaidAsync(admin, invoiceId, 4m);
        var itemId = await FirstItemIdAsync(admin, invoiceId);

        var response = await admin.PutAsJsonAsync($"/api/billing/{invoiceId}/items", new
        {
            items = new[] { new { id = (int?)itemId, description = "Hosting", unitPrice = 3m, quantity = 1, isDeleted = false } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.INVOICE_TOTAL_BELOW_PAID);
        var reFetched = await admin.GetAsync($"/api/billing/{invoiceId}");
        using var body = JsonDocument.Parse(await reFetched.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("amounts").GetProperty("subTotal").GetDecimal().Should().Be(10m);
    }

    /// <summary>
    /// A line change that leaves the payments already taken covering the invoice settles it in the
    /// same save: nothing is owed, so it does not stay open and go overdue.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateItemsDownToThePaymentsTakenSettlesTheInvoice()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 10m);
        await BillingTestSeeds.MarkPaidAsync(admin, invoiceId, 4m);
        var itemId = await FirstItemIdAsync(admin, invoiceId);

        var response = await admin.PutAsJsonAsync($"/api/billing/{invoiceId}/items", new
        {
            items = new[] { new { id = (int?)itemId, description = "Hosting", unitPrice = 4m, quantity = 1, isDeleted = false } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var body = JsonDocument.Parse(await (await admin.GetAsync($"/api/billing/{invoiceId}")).Content.ReadAsStringAsync());
        body.RootElement.GetProperty("status").GetString().Should().Be(nameof(InvoiceStatus.Paid));
        body.RootElement.GetProperty("amountDue").GetDecimal().Should().Be(0m);
    }
}
