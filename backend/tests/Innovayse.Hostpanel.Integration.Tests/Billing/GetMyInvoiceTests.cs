using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Billing;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>GET /api/me/invoices/{id}</c> against Postgres: a client reads one of their own invoices,
/// scoped through <c>IInvoiceOwnership</c>, which answers <c>INVOICE_NOT_FOUND</c> alike for a
/// stranger's invoice and for one that does not exist at all.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetMyInvoiceTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The owner reads their own invoice by id.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetById_TheOwnersInvoice_Returns200()
    {
        var (owner, ownerClientId) = await BillingTestSeeds.RegisterClientAsync(factory, null, "owner-get");
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, ownerClientId, 10m);

        var response = await owner.GetAsync($"/api/me/invoices/{invoiceId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("id").GetInt32().Should().Be(invoiceId);
    }

    /// <summary>
    /// The client's own invoice carries no staff note, no gateway reference and no gateway fee,
    /// though the invoice has all three, and neither does the same invoice in the list.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetById_CarriesNoStaffNoteOrGatewayReference()
    {
        var (owner, ownerClientId) = await BillingTestSeeds.RegisterClientAsync(factory, null, "owner-no-internals");
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, ownerClientId, 10m);
        const string note = "staff only invoice note";
        var reference = $"gw-ref-{Guid.NewGuid():N}";
        (await admin.PutAsJsonAsync($"/api/billing/{invoiceId}/notes", new { notes = note }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/payment", new
        {
            date = DateTimeOffset.UtcNow,
            gateway = PaymentMethodNames.Manual,
            transactionId = reference,
            amount = 10m,
            fees = 0.5m,
        })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var single = await (await owner.GetAsync($"/api/me/invoices/{invoiceId}")).Content.ReadAsStringAsync();
        var list = await (await owner.GetAsync("/api/me/invoices")).Content.ReadAsStringAsync();

        foreach (var json in new[] { single, list })
        {
            json.Should().NotContain(note).And.NotContain(reference);
            json.Should().NotContain("\"gatewayTransactionId\"").And.NotContain("\"fees\"")
                .And.NotContain("\"notes\"").And.NotContain("\"gateway\"").And.NotContain("\"clientId\"");
        }

        using var body = JsonDocument.Parse(single);
        body.RootElement.GetProperty("transactions").EnumerateArray().Single()
            .GetProperty("amount").GetDecimal().Should().Be(10m);
    }

    /// <summary>
    /// After a partial payment the client's invoice carries what is still owed, in the single read
    /// and in the list alike, so the pay button charges the rest and not the whole total again.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetById_AfterPartialPayment_CarriesTheAmountStillDue()
    {
        var (owner, ownerClientId) = await BillingTestSeeds.RegisterClientAsync(factory, null, "owner-amount-due");
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, ownerClientId, 10m);
        (await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/payment", new
        {
            date = DateTimeOffset.UtcNow,
            gateway = PaymentMethodNames.Manual,
            transactionId = $"part-{Guid.NewGuid():N}",
            amount = 4m,
            fees = 0m,
        })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var single = await (await owner.GetAsync($"/api/me/invoices/{invoiceId}")).Content.ReadFromJsonAsync<JsonElement>();
        var list = await (await owner.GetAsync("/api/me/invoices")).Content.ReadFromJsonAsync<JsonElement>();

        single.GetProperty("amountDue").GetDecimal().Should().Be(6m);
        single.GetProperty("total").GetDecimal().Should().Be(10m);
        list.EnumerateArray().Single(i => i.GetProperty("id").GetInt32() == invoiceId)
            .GetProperty("amountDue").GetDecimal().Should().Be(6m);
    }

    /// <summary>Reading a stranger's invoice is refused with <c>INVOICE_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetById_AStrangersInvoice_Returns404WithInvoiceNotFound()
    {
        var (_, ownerClientId) = await BillingTestSeeds.RegisterClientAsync(factory, null, "owner-strangers-get");
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, ownerClientId, 10m);
        var stranger = await BillingTestSeeds.RegisterStrangerAsync(factory, null, "stranger-get");

        var response = await stranger.GetAsync($"/api/me/invoices/{invoiceId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.INVOICE_NOT_FOUND);
    }

    /// <summary>
    /// Another client's invoice is not merely refused: it is indistinguishable from one that
    /// does not exist. An attacker walking sequential invoice ids must not learn which ones are
    /// real from the status alone.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetById_AnUnknownInvoice_AnswersIdenticallyToAStrangersInvoice()
    {
        var (_, ownerClientId) = await BillingTestSeeds.RegisterClientAsync(factory, null, "owner-strangers-cmp");
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, ownerClientId, 10m);
        var stranger = await BillingTestSeeds.RegisterStrangerAsync(factory, null, "stranger-cmp");

        var response = await stranger.GetAsync($"/api/me/invoices/{invoiceId}");
        var absent = await stranger.GetAsync($"/api/me/invoices/{BillingTestSeeds.NoSuchId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        absent.StatusCode.Should().Be(response.StatusCode);
        (await absent.Content.ReadAsStringAsync()).Should().Be(await response.Content.ReadAsStringAsync());
    }

    /// <summary>An id naming no invoice at all answers exactly like a stranger's invoice.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetById_AnUnknownInvoice_Returns404WithInvoiceNotFound()
    {
        var (client, _) = await BillingTestSeeds.RegisterClientAsync(factory, null, "unknown-get");

        var response = await client.GetAsync($"/api/me/invoices/{BillingTestSeeds.NoSuchId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.INVOICE_NOT_FOUND);
    }

    /// <summary>Reading an invoice is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetByIdRefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/me/invoices/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
