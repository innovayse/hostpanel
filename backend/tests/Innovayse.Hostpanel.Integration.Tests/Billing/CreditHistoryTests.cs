using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Billing.Common.Enums;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// The credit history on the client ledger, one case per movement of the balance: each is written in the same save as the
/// balance with its type, its signed change and the balance it left; entries that already hold the cash are stamped instead
/// of doubled; credit-only lines stay out of every cash view; and nothing older than the history is invented.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CreditHistoryTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An admin transaction that adds to credit is stamped as the adjustment: the cash entry is the history line.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminTransactionAddedToCredit_IsOneStampedEntry()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);

        await BillingTestSeeds.CreateTransactionAsync(admin, clientId, 40m, addToCredit: true);

        var ledger = await CreditTestSeeds.LedgerAsync(factory, clientId);
        var entry = ledger.Should().ContainSingle().Subject;
        entry.CreditMovement.Should().Be(CreditMovementType.AdminAdd);
        entry.CreditChange.Should().Be(40m);
        entry.BalanceAfter.Should().Be(40m);
        entry.AmountIn.Should().Be(40m);
        entry.AddedToCredit.Should().BeTrue();
    }

    /// <summary>A transaction that does not touch the balance leaves the history alone.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminTransactionNotAddedToCredit_IsNoHistory()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);

        await BillingTestSeeds.CreateTransactionAsync(admin, clientId, 40m, addToCredit: false);

        (await CreditTestSeeds.HistoryAsync(factory, clientId)).Should().BeEmpty();
        (await CreditTestSeeds.LedgerAsync(factory, clientId)).Should().ContainSingle().Which.CreditMovement.Should().BeNull();
    }

    /// <summary>An admin transaction that takes from credit is stamped as the deduction.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminTransactionTakenFromCredit_IsOneStampedEntry()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        await BillingTestSeeds.GiveCreditAsync(factory, clientId, 25m);

        var created = await admin.PostAsJsonAsync("/api/transactions", new
        {
            clientId,
            date = TestMoments.Seeded,
            description = "Cash out",
            transactionId = $"txn-{Guid.NewGuid():N}",
            invoiceId = (int?)null,
            paymentMethod = PaymentMethodNames.Manual,
            amountIn = 0m,
            amountOut = 10m,
            fees = 0m,
            addToCredit = true,
        });

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var entry = (await CreditTestSeeds.HistoryAsync(factory, clientId)).Should().ContainSingle().Subject;
        entry.CreditMovement.Should().Be(CreditMovementType.AdminDeduct);
        entry.CreditChange.Should().Be(-10m);
        entry.BalanceAfter.Should().Be(15m);
        entry.AmountOut.Should().Be(10m);
    }

    /// <summary>Removing credit from an invoice hands it back to the balance as a credit-only line tied to the invoice.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RemovingCreditFromAnInvoice_IsAReturnLine()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        await BillingTestSeeds.GiveCreditAsync(factory, clientId, 30m);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 50m);
        (await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/credit", new { amount = 20m })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var removed = await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/credit/remove", new { amount = 5m });

        removed.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var history = await CreditTestSeeds.HistoryAsync(factory, clientId);
        history.Select(t => (t.CreditMovement, t.CreditChange, t.BalanceAfter, t.InvoiceId)).Should().BeEquivalentTo(
            new (CreditMovementType?, decimal?, decimal?, int?)[]
            {
                (CreditMovementType.InvoiceApplied, -20m, 10m, invoiceId),
                (CreditMovementType.InvoiceReturned, 5m, 15m, invoiceId),
            });
    }

    /// <summary>Cancelling an invoice that holds credit returns all of it to the balance, on the history.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CancellingAnInvoiceWithCredit_ReturnsItOnTheHistory()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        await BillingTestSeeds.GiveCreditAsync(factory, clientId, 30m);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 50m);
        await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/credit", new { amount = 20m });

        var cancelled = await admin.PostAsync($"/api/billing/{invoiceId}/cancel", content: null);

        cancelled.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(30m);
        var history = await CreditTestSeeds.HistoryAsync(factory, clientId);
        history.Last().CreditMovement.Should().Be(CreditMovementType.InvoiceReturned);
        history.Last().CreditChange.Should().Be(20m);
        history.Last().BalanceAfter.Should().Be(30m);
    }

    /// <summary>A refund paid back as credit stamps the refund's own cash entry: one line, the cash going out and the credit coming in.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefundToCredit_IsOneStampedRefundEntry()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 30m);
        await BillingTestSeeds.MarkPaidAsync(admin, invoiceId, 30m);

        var refunded = await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/refund", new
        {
            amount = 12m,
            refundType = nameof(RefundType.CreditBalance),
        });

        refunded.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var entry = (await CreditTestSeeds.HistoryAsync(factory, clientId)).Should().ContainSingle().Subject;
        entry.CreditMovement.Should().Be(CreditMovementType.RefundToCredit);
        entry.CreditChange.Should().Be(12m);
        entry.BalanceAfter.Should().Be(12m);
        entry.AmountOut.Should().Be(12m);
        entry.AddedToCredit.Should().BeTrue();
    }

    /// <summary>Every movement of the balance adds up: the history of a mixed run is exactly the balance, line by line.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AMixedRun_AddsUpLineByLine()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        await admin.PostAsJsonAsync($"/api/clients/{clientId}/credit/adjustments", new { direction = "Add", amount = 100m, reason = "Start" });
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 50m);
        await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/credit", new { amount = 35m });
        await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/credit/remove", new { amount = 10m });
        await BillingTestSeeds.CreateTransactionAsync(admin, clientId, 7m, addToCredit: true);
        await admin.PostAsJsonAsync($"/api/clients/{clientId}/credit/adjustments", new { direction = "Deduct", amount = 3m, reason = "Fix" });

        var history = await CreditTestSeeds.HistoryAsync(factory, clientId);

        history.Select(t => t.CreditChange).Should().Equal(100m, -35m, 10m, 7m, -3m);
        history.Select(t => t.BalanceAfter).Should().Equal(100m, 65m, 75m, 82m, 79m);
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(79m);
    }

    /// <summary>
    /// Credit-only lines move no cash, so the cash ledger list shows only the entries that moved money and its totals ignore
    /// the rest.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CreditOnlyLines_StayOutOfTheCashLedger()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        await admin.PostAsJsonAsync($"/api/clients/{clientId}/credit/adjustments", new { direction = "Add", amount = 100m, reason = "Goodwill" });
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 50m);
        await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/credit", new { amount = 20m });
        await BillingTestSeeds.CreateTransactionAsync(admin, clientId, 8m, addToCredit: false);

        var ledger = await admin.GetFromJsonAsync<JsonElement>($"/api/transactions/client/{clientId}");

        var rows = ledger.GetProperty("transactions").GetProperty("items");
        rows.GetArrayLength().Should().Be(1);
        rows[0].GetProperty("amountIn").GetDecimal().Should().Be(8m);
        ledger.GetProperty("totalIn").GetDecimal().Should().Be(8m);
        (await CreditTestSeeds.HistoryAsync(factory, clientId)).Should().HaveCount(2);
    }

    /// <summary>Entries older than the history carry no movement and are not shown in it; the history starts where the feature did.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task OlderEntries_AreNotInventedIntoTheHistory()
    {
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        await BillingTestSeeds.SeedLegacyCreditEntryAsync(factory, clientId, 10m);

        (await CreditTestSeeds.HistoryAsync(factory, clientId)).Should().BeEmpty();
        var ledger = await CreditTestSeeds.LedgerAsync(factory, clientId);
        ledger.Should().ContainSingle().Which.CreditMovement.Should().BeNull();
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(10m);
    }
}
