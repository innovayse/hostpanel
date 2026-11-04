using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.SDK.Plugins.Enums;
using Innovayse.SDK.Plugins.StoredCredentials;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <see cref="InvoicePaymentClaimGate.ReconcileStaleAsync"/> against Postgres, the pass the gateway reconcile job
/// runs: an unfinished claim past its lifetime is looked up at the gateway whatever became of its invoice or consent
/// meanwhile, and ends only when the money is accounted for.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class InvoicePaymentClaimGateTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The saved card the seeded clients own.</summary>
    private const string CardId = "pm_gate_1";

    /// <summary>One seeded stale claim: the client, the invoice, the claim's key and the gateway customer.</summary>
    /// <param name="ClientId">The client.</param>
    /// <param name="InvoiceId">The invoice.</param>
    /// <param name="Key">The claim's gateway key.</param>
    /// <param name="CustomerRef">The client's account at the fake module.</param>
    private sealed record StaleClaim(int ClientId, int InvoiceId, string Key, string CustomerRef);

    /// <summary>Builds a host with the capability and a configured one-off card gateway that refunds what settlement hands back.</summary>
    /// <param name="card">The fake one-off card gateway.</param>
    /// <returns>The host with its fake capability.</returns>
    private StoredCredentialHost BuildHost(FakeCardPaymentGateway card) => StoredCredentialHost.Create(factory, card);

    /// <summary>Seeds a client with a customer, an unpaid invoice of 10 and a claim on it that is long past its lifetime.</summary>
    /// <param name="gateway">The fake capability host.</param>
    /// <param name="label">Distinguishes the seeded email between tests.</param>
    /// <param name="withCustomer">Whether the client has an account at the module.</param>
    /// <returns>The seeded claim.</returns>
    private async Task<StaleClaim> SeedAsync(StoredCredentialHost gateway, string label, bool withCustomer = true)
    {
        var (_, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, gateway.Host, label);
        var customerRef = $"cus_gate_{Guid.NewGuid():N}"[..24];
        if (withCustomer)
        {
            await gateway.LinkAsync(clientId, customerRef);
        }

        var admin = await BillingTestSeeds.AdminClientAsync(factory, gateway.Host);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 10m);
        var key = InvoicePaymentClaim.AutomaticKey(invoiceId, Guid.NewGuid());
        using var scope = gateway.Host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        context.InvoicePaymentClaims.Add(InvoicePaymentClaim.Create(
            invoiceId, StoredCredentialHost.Module, InvoicePaymentClaimKind.Automatic, key, DateTimeOffset.UtcNow.AddHours(-3), TimeSpan.FromMinutes(15)));
        await context.SaveChangesAsync(CancellationToken.None);
        return new StaleClaim(clientId, invoiceId, key, customerRef);
    }

    /// <summary>Records a charge the gateway holds under the claim's key.</summary>
    /// <param name="gateway">The fake capability host.</param>
    /// <param name="claim">The seeded claim.</param>
    /// <param name="result">What the gateway did with the charge.</param>
    private static void RecordCharge(StoredCredentialHost gateway, StaleClaim claim, StoredChargeResult result) =>
        gateway.Plugin.Charges[claim.Key] = (
            new StoredChargeInstruction(claim.CustomerRef, CardId, 1000, "USD", claim.InvoiceId.ToString(System.Globalization.CultureInfo.InvariantCulture), claim.Key, null),
            result);

    /// <summary>A charge that went through for the given reference.</summary>
    /// <param name="transactionId">The gateway reference.</param>
    /// <returns>The charge result.</returns>
    private static StoredChargeResult Taken(string transactionId) => new(StoredChargeOutcome.Succeeded, transactionId, 1000, "USD", null);

    /// <summary>Runs one reconcile pass on a fresh scope.</summary>
    /// <param name="host">The test host.</param>
    /// <returns>A task that completes when the pass has run.</returns>
    private static async Task ReconcileAsync(WebApplicationFactory<Program> host)
    {
        using var scope = host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<InvoicePaymentClaimGate>().ReconcileStaleAsync(CancellationToken.None);
    }

    /// <summary>Reads the outcome of a claim.</summary>
    /// <param name="host">The test host.</param>
    /// <param name="key">The claim's gateway key.</param>
    /// <returns>The outcome, or null while the claim is open.</returns>
    private static async Task<InvoicePaymentClaimOutcome?> OutcomeOfAsync(WebApplicationFactory<Program> host, string key)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.InvoicePaymentClaims.AsNoTracking().Where(c => c.GatewayIdempotencyKey == key).Select(c => c.Outcome).SingleAsync();
    }

    /// <summary>Counts the ledger rows that carry a gateway reference, with or without an invoice.</summary>
    /// <param name="host">The test host.</param>
    /// <param name="transactionId">The gateway reference.</param>
    /// <returns>The number of rows.</returns>
    private static async Task<int> LedgerRowsOfAsync(WebApplicationFactory<Program> host, string transactionId)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.Transactions.AsNoTracking().CountAsync(t => t.TransactionId == transactionId);
    }

    /// <summary>Reads an invoice's status.</summary>
    /// <param name="host">The test host.</param>
    /// <param name="invoiceId">The invoice.</param>
    /// <returns>The status.</returns>
    private static async Task<InvoiceStatus> StatusOfAsync(WebApplicationFactory<Program> host, int invoiceId)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.Invoices.AsNoTracking().Where(i => i.Id == invoiceId).Select(i => i.Status).SingleAsync();
    }

    /// <summary>A charge that went through is recorded and the claim settled, even though the client's agreement was withdrawn meanwhile; a second pass records nothing more.</summary>
    [Fact]
    public async Task ReconcileStale_ChargeWentThrough_RecordsItOnceEvenAfterTheAgreementWasRevokedAsync()
    {
        var gateway = BuildHost(new FakeCardPaymentGateway { IsConfigured = true });
        var claim = await SeedAsync(gateway, "gate-settled");
        await gateway.AddConsentAsync(claim.ClientId, CardId);
        using (var scope = gateway.Host.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            await context.CardChargeConsents.Where(c => c.ClientId == claim.ClientId)
                .ExecuteUpdateAsync(set => set.SetProperty(c => c.Status, CardConsentStatus.Revoked));
        }

        var txn = $"txn-gate-{Guid.NewGuid():N}";
        RecordCharge(gateway, claim, Taken(txn));

        await ReconcileAsync(gateway.Host);
        await ReconcileAsync(gateway.Host);

        (await OutcomeOfAsync(gateway.Host, claim.Key)).Should().Be(InvoicePaymentClaimOutcome.Settled);
        (await StatusOfAsync(gateway.Host, claim.InvoiceId)).Should().Be(InvoiceStatus.Paid);
        (await LedgerRowsOfAsync(gateway.Host, txn)).Should().Be(1);
        gateway.Plugin.Charges.Should().ContainSingle();
    }

    /// <summary>
    /// A charge went through for an invoice that was cancelled meanwhile: the pass still reconciles it, the payment is
    /// handed back once (refunded) and the claim ends as failed, never as settled.
    /// </summary>
    [Fact]
    public async Task ReconcileStale_InvoiceCancelledMeanwhile_HandsTheMoneyBackOnceAndFailsTheClaimAsync()
    {
        var card = new FakeCardPaymentGateway { IsConfigured = true };
        var gateway = BuildHost(card);
        var claim = await SeedAsync(gateway, "gate-cancelled");
        var txn = $"txn-gate-{Guid.NewGuid():N}";
        RecordCharge(gateway, claim, Taken(txn));
        using (var scope = gateway.Host.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            await context.Invoices.Where(i => i.Id == claim.InvoiceId).ExecuteUpdateAsync(set => set.SetProperty(i => i.Status, InvoiceStatus.Cancelled));
        }

        await ReconcileAsync(gateway.Host);
        await ReconcileAsync(gateway.Host);

        (await OutcomeOfAsync(gateway.Host, claim.Key)).Should().Be(InvoicePaymentClaimOutcome.Failed);
        card.RefundInstructions.Should().ContainSingle(r => r.TransactionId == txn);
        (await LedgerRowsOfAsync(gateway.Host, txn)).Should().Be(1);
        (await StatusOfAsync(gateway.Host, claim.InvoiceId)).Should().Be(InvoiceStatus.Cancelled);
    }

    /// <summary>A lookup the gateway cannot answer keeps the claim open: nothing is known about the money.</summary>
    [Fact]
    public async Task ReconcileStale_LookupFails_KeepsTheClaimOpenAsync()
    {
        var gateway = BuildHost(new FakeCardPaymentGateway { IsConfigured = true });
        var claim = await SeedAsync(gateway, "gate-lookup");
        gateway.Plugin.LookupThrows = true;

        await ReconcileAsync(gateway.Host);

        (await OutcomeOfAsync(gateway.Host, claim.Key)).Should().BeNull();
    }

    /// <summary>A gateway with no charge under the key, answering the lookup successfully, expires the claim.</summary>
    [Fact]
    public async Task ReconcileStale_NoChargeUnderTheKey_ExpiresTheClaimAsync()
    {
        var gateway = BuildHost(new FakeCardPaymentGateway { IsConfigured = true });
        var claim = await SeedAsync(gateway, "gate-none");

        await ReconcileAsync(gateway.Host);

        (await OutcomeOfAsync(gateway.Host, claim.Key)).Should().Be(InvoicePaymentClaimOutcome.Expired);
        (await StatusOfAsync(gateway.Host, claim.InvoiceId)).Should().Be(InvoiceStatus.Unpaid);
    }

    /// <summary>A charge the gateway declined ends the claim as failed.</summary>
    [Fact]
    public async Task ReconcileStale_ChargeDeclined_FailsTheClaimAsync()
    {
        var gateway = BuildHost(new FakeCardPaymentGateway { IsConfigured = true });
        var claim = await SeedAsync(gateway, "gate-declined");
        RecordCharge(gateway, claim, new StoredChargeResult(StoredChargeOutcome.Declined, null, null, null, null));

        await ReconcileAsync(gateway.Host);

        (await OutcomeOfAsync(gateway.Host, claim.Key)).Should().Be(InvoicePaymentClaimOutcome.Failed);
    }

    /// <summary>A charge the gateway is still processing keeps the claim open.</summary>
    [Fact]
    public async Task ReconcileStale_ChargePending_KeepsTheClaimOpenAsync()
    {
        var gateway = BuildHost(new FakeCardPaymentGateway { IsConfigured = true });
        var claim = await SeedAsync(gateway, "gate-pending");
        RecordCharge(gateway, claim, new StoredChargeResult(StoredChargeOutcome.Pending, null, null, null, null));

        await ReconcileAsync(gateway.Host);

        (await OutcomeOfAsync(gateway.Host, claim.Key)).Should().BeNull();
    }

    /// <summary>A claim whose client has no account at the module cannot be looked up and stays open for an operator.</summary>
    [Fact]
    public async Task ReconcileStale_NoGatewayAccount_KeepsTheClaimOpenAsync()
    {
        var gateway = BuildHost(new FakeCardPaymentGateway { IsConfigured = true });
        var claim = await SeedAsync(gateway, "gate-no-account", withCustomer: false);

        await ReconcileAsync(gateway.Host);

        (await OutcomeOfAsync(gateway.Host, claim.Key)).Should().BeNull();
    }

    /// <summary>
    /// The charge was already handed back (a ledger row with no invoice carries its reference): that row is not a
    /// recorded payment. The claim ends as failed because the money is accounted for, never as settled.
    /// </summary>
    [Fact]
    public async Task ReconcileStale_ChargeAlreadyHandedBack_IsNotCountedAsRecordedAsync()
    {
        var gateway = BuildHost(new FakeCardPaymentGateway { IsConfigured = true });
        var claim = await SeedAsync(gateway, "gate-handed-back");
        var txn = $"txn-gate-{Guid.NewGuid():N}";
        RecordCharge(gateway, claim, Taken(txn));
        using (var scope = gateway.Host.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            context.Transactions.Add(Transaction.Create(
                claim.ClientId, DateTimeOffset.UtcNow, "Card payment refunded", txn, null, PaymentMethodNames.Card, 10m, 10m, 0m,
                addedToCredit: false, "USD", TransactionSource.GatewayPayment));
            await context.SaveChangesAsync(CancellationToken.None);
        }

        await ReconcileAsync(gateway.Host);

        (await OutcomeOfAsync(gateway.Host, claim.Key)).Should().Be(InvoicePaymentClaimOutcome.Failed);
        (await StatusOfAsync(gateway.Host, claim.InvoiceId)).Should().Be(InvoiceStatus.Unpaid);
    }

    /// <summary>The same replay of a payment held for an operator is not accounted for either: the claim stays open.</summary>
    [Fact]
    public async Task ReconcileStale_ChargeHeldForTheOperator_KeepsTheClaimOpenAsync()
    {
        var gateway = BuildHost(new FakeCardPaymentGateway { IsConfigured = true });
        var claim = await SeedAsync(gateway, "gate-held");
        var txn = $"txn-gate-{Guid.NewGuid():N}";
        RecordCharge(gateway, claim, Taken(txn));
        using (var scope = gateway.Host.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            context.Transactions.Add(Transaction.Create(
                claim.ClientId, DateTimeOffset.UtcNow, "Card payment held for operator review", txn, null, PaymentMethodNames.Card, 10m, 0m, 0m,
                addedToCredit: false, "USD", TransactionSource.GatewayPayment));
            await context.SaveChangesAsync(CancellationToken.None);
        }

        await ReconcileAsync(gateway.Host);

        (await OutcomeOfAsync(gateway.Host, claim.Key)).Should().BeNull();
    }
}
