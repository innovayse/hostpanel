using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Shared.Constants;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>GET /api/me/invoices/{id}/bank-transfer</c> against Postgres: the client reads the bank details and the reference of the
/// transfer waiting on their own invoice, and only while one waits.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetMyBankTransferTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The owner reads the details of a waiting transfer, with no client id in them.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Get_TheOwnersWaitingTransfer_AnswersTheDetails()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        await BankTransferTestSeeds.EnableAsync(admin);
        var (owner, clientId, _, _) = await BillingTestSeeds.RegisterClientAsync(factory, null, "bt-get");
        var invoiceId = await BankTransferTestSeeds.PendingInvoiceAsync(admin, owner, clientId, 55m);

        var response = await owner.GetAsync($"/api/me/invoices/{invoiceId}/bank-transfer");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("reference").GetString().Should().Be($"INV-{invoiceId}");
        body.GetProperty("iban").GetString().Should().Be(BankTransferTestSeeds.Iban);
        body.GetProperty("amountDue").GetDecimal().Should().Be(55m);
        body.EnumerateObject().Select(p => p.Name).Should().NotContain(name => name.Contains("client", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>An invoice that never chose a transfer shows no bank details: 409 <c>INVOICE_BANK_TRANSFER_NOT_PENDING</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Get_WhenNoTransferWaits_Returns409()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        await BankTransferTestSeeds.EnableAsync(admin);
        var (owner, clientId, _, _) = await BillingTestSeeds.RegisterClientAsync(factory, null, "bt-get-none");
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 55m);

        var response = await owner.GetAsync($"/api/me/invoices/{invoiceId}/bank-transfer");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.INVOICE_BANK_TRANSFER_NOT_PENDING);
    }

    /// <summary>Another client's invoice answers like one that does not exist.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Get_AStrangersInvoice_Returns404()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        await BankTransferTestSeeds.EnableAsync(admin);
        var (owner, ownerClientId, _, _) = await BillingTestSeeds.RegisterClientAsync(factory, null, "bt-get-owner");
        var invoiceId = await BankTransferTestSeeds.PendingInvoiceAsync(admin, owner, ownerClientId, 55m);
        var stranger = await BillingTestSeeds.RegisterStrangerAsync(factory, null, "bt-get-stranger");

        var response = await stranger.GetAsync($"/api/me/invoices/{invoiceId}/bank-transfer");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.INVOICE_NOT_FOUND);
    }

    /// <summary>The read is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetRefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/me/invoices/1/bank-transfer");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A non-positive id is refused by the validator with 400 <c>VALIDATION_ERROR</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Get_WithNonPositiveId_Returns400ValidationError()
    {
        var (client, _, _, _) = await BillingTestSeeds.RegisterClientAsync(factory, null, "bt-get-validation");

        var response = await client.GetAsync("/api/me/invoices/0/bank-transfer");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
    }
}
