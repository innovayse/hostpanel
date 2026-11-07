using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>DELETE /api/transactions/{id}</c> against Postgres: deleting a transaction reverses any
/// credit it had added, the <c>CLIENT_CREDIT_INSUFFICIENT</c> refusal when that credit is already
/// spent, and the <c>TRANSACTION_NOT_FOUND</c> refusal for an id that names no
/// transaction.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class DeleteTransactionTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Deleting a transaction reverses the credit it had added, then removes it from the ledger.</summary>
    [Fact]
    public async Task DeleteReversesCreditThenRemovesTheTransaction()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var transactionId = await BillingTestSeeds.SeedLegacyCreditEntryAsync(factory, clientId, 10.00m);

        var deleted = await admin.DeleteAsync($"/api/transactions/{transactionId}");
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var fetched = await admin.GetAsync($"/api/transactions/{transactionId}");
        fetched.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Deleting an entry older than the credit history undoes its credit move and writes that undoing to the history, so the
    /// history still adds up to what the balance moved by since.
    /// </summary>
    [Fact]
    public async Task DeletingALegacyEntry_RecordsTheReversal()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var transactionId = await BillingTestSeeds.SeedLegacyCreditEntryAsync(factory, clientId, 10.00m);

        var deleted = await admin.DeleteAsync($"/api/transactions/{transactionId}");

        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(0m);
        var history = await CreditTestSeeds.HistoryAsync(factory, clientId);
        var line = history.Should().ContainSingle().Subject;
        line.CreditMovement.Should().Be(CreditMovementType.TransactionReversed);
        line.CreditChange.Should().Be(-10m);
        line.BalanceAfter.Should().Be(0m);
        line.MovesMoney.Should().BeFalse();
    }

    /// <summary>
    /// An entry written after the credit history existed is part of it and is not deleted: a delete would take the line out
    /// of the history while the balance moved, so the answer is 409 and nothing changes.
    /// </summary>
    [Fact]
    public async Task DeletingAnEntryOfTheHistory_IsRefused()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var transactionId = await BillingTestSeeds.CreateTransactionAsync(admin, clientId, 10.00m, addToCredit: true);

        var response = await admin.DeleteAsync($"/api/transactions/{transactionId}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.TRANSACTION_CREDIT_LOCKED);
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(10m);
        (await admin.GetAsync($"/api/transactions/{transactionId}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Deleting a transaction whose credit has already been spent answers 400 with
    /// <c>CLIENT_CREDIT_INSUFFICIENT</c> and keeps both the transaction and the balance.
    /// </summary>
    [Fact]
    public async Task DeleteRefusesWhenTheAddedCreditIsSpent()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var transactionId = await BillingTestSeeds.SeedLegacyCreditEntryAsync(factory, clientId, 10.00m);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 19.99m);
        var applied = await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/credit", new { amount = 8m });
        applied.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await admin.DeleteAsync($"/api/transactions/{transactionId}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.CLIENT_CREDIT_INSUFFICIENT);
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(2m);
        var fetched = await admin.GetAsync($"/api/transactions/{transactionId}");
        fetched.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>Deleting an id that names no transaction answers 404 with <c>TRANSACTION_NOT_FOUND</c>.</summary>
    [Fact]
    public async Task DeleteUnknownTransactionRefusesNotFound()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.DeleteAsync($"/api/transactions/{BillingTestSeeds.NoSuchId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.TRANSACTION_NOT_FOUND);
    }

    /// <summary>Deleting a transaction is not for anonymous callers.</summary>
    [Fact]
    public async Task DeleteRefusesAnonymous()
    {
        var response = await factory.CreateClient().DeleteAsync("/api/transactions/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
