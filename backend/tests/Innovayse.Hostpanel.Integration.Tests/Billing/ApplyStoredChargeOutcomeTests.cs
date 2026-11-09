using Hostpanel.Sdk.Plugins.Enums;
using Innovayse.Hostpanel.Application.Billing.Commands.ApplyStoredChargeOutcome;
using Innovayse.Hostpanel.Application.Billing.Commands.ChargeInvoiceStoredMethod;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <see cref="ApplyStoredChargeOutcomeCommand"/> through the real bus against Postgres: how a reported outcome ends a
/// claim and, for a success, the invoice. Every claim here is first opened by a charge whose outcome is unknown (an
/// amount ending in 05), so the claim is unresolved when the outcome is applied.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ApplyStoredChargeOutcomeTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>ISO 4217 numeric code of USD, the currency every invoice here is issued in.</summary>
    private const string Usd = "840";

    /// <summary>The amount of the seeded invoices in minor units.</summary>
    private const long AmountMinor = 1005;

    /// <summary>Opens an unresolved claim on a fresh invoice for a consenting client.</summary>
    /// <param name="scenario">The scenario to seed.</param>
    /// <returns>The client, the invoice and the open claim.</returns>
    private static async Task<(int ClientId, int InvoiceId, InvoicePaymentClaim Claim)> OpenClaimAsync(StoredCredentialScenario scenario)
    {
        var clientId = await scenario.SeedClientAsync();
        await scenario.SeedConsentAsync(clientId);
        var invoiceId = await scenario.SeedInvoiceAsync(clientId, 10.05m);
        await scenario.InvokeAsync<Result<InvoiceChargeDto>>(new ChargeInvoiceStoredMethodCommand(invoiceId));
        var claim = (await scenario.ClaimsOfAsync(invoiceId)).Single();
        claim.Status.Should().Be(InvoicePaymentClaimStatus.Open);
        return (clientId, invoiceId, claim);
    }

    /// <summary>A success for exactly what was asked settles the invoice and the claim in one step.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Apply_MatchingSuccess_SettlesTheInvoiceAndTheClaim()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var (_, invoiceId, claim) = await OpenClaimAsync(scenario);

        var result = await scenario.InvokeAsync<Result<InvoiceChargeDto>>(
            new ApplyStoredChargeOutcomeCommand(claim.Id, StoredChargeOutcome.Succeeded, "txn-ok", AmountMinor, Usd, null));

        result.Value!.Status.Should().Be(InvoicePaymentClaimStatus.Settled);
        var invoice = await scenario.InvoiceAsync(invoiceId);
        invoice.IsPaid.Should().BeTrue();
        invoice.Transactions.Should().ContainSingle(t => t.TransactionId == "txn-ok");
    }

    /// <summary>Applying the same outcome twice records the payment once and answers the settled claim.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Apply_Twice_RecordsThePaymentOnce()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var (_, invoiceId, claim) = await OpenClaimAsync(scenario);
        var command = new ApplyStoredChargeOutcomeCommand(claim.Id, StoredChargeOutcome.Succeeded, "txn-twice", AmountMinor, Usd, null);

        await scenario.InvokeAsync<Result<InvoiceChargeDto>>(command);
        var replay = await scenario.InvokeAsync<Result<InvoiceChargeDto>>(command);

        replay.Value!.Status.Should().Be(InvoicePaymentClaimStatus.Settled);
        (await scenario.InvoiceAsync(invoiceId)).Transactions.Should().ContainSingle(t => t.TransactionId == "txn-twice");
    }

    /// <summary>A success reported for another amount is not believed: the claim is held and nothing is recorded.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Apply_SuccessForAnotherAmount_HoldsTheClaim()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var (_, invoiceId, claim) = await OpenClaimAsync(scenario);

        var result = await scenario.InvokeAsync<Result<InvoiceChargeDto>>(
            new ApplyStoredChargeOutcomeCommand(claim.Id, StoredChargeOutcome.Succeeded, "txn-wrong", AmountMinor - 1, Usd, null));

        result.Value!.Status.Should().Be(InvoicePaymentClaimStatus.Held);
        (await scenario.InvoiceAsync(invoiceId)).IsPaid.Should().BeFalse();
    }

    /// <summary>A success reported in another currency, or with no transaction, is held the same way.</summary>
    /// <param name="currency">The currency the gateway reports.</param>
    /// <param name="transactionId">The transaction the gateway reports.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("978", "txn-eur")]
    [InlineData(null, "txn-nocurrency")]
    [InlineData(Usd, null)]
    public async Task Apply_SuccessWithAnotherCurrencyOrNoTransaction_HoldsTheClaim(string? currency, string? transactionId)
    {
        using var scenario = new StoredCredentialScenario(factory);
        var (_, invoiceId, claim) = await OpenClaimAsync(scenario);

        var result = await scenario.InvokeAsync<Result<InvoiceChargeDto>>(
            new ApplyStoredChargeOutcomeCommand(claim.Id, StoredChargeOutcome.Succeeded, transactionId, AmountMinor, currency, null));

        result.Value!.Status.Should().Be(InvoicePaymentClaimStatus.Held);
        (await scenario.InvoiceAsync(invoiceId)).IsPaid.Should().BeFalse();
    }

    /// <summary>Money taken for an invoice that was paid meanwhile is held for an operator, not recorded twice.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Apply_SuccessForAnInvoiceThatWasPaidMeanwhile_HoldsTheClaim()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var (_, invoiceId, claim) = await OpenClaimAsync(scenario);
        await scenario.AddPaymentAsync(invoiceId, 10.05m);

        var result = await scenario.InvokeAsync<Result<InvoiceChargeDto>>(
            new ApplyStoredChargeOutcomeCommand(claim.Id, StoredChargeOutcome.Succeeded, "txn-late", AmountMinor, Usd, null));

        result.Value!.Status.Should().Be(InvoicePaymentClaimStatus.Held);
        (await scenario.InvoiceAsync(invoiceId)).Transactions.Should().NotContain(t => t.TransactionId == "txn-late");
    }

    /// <summary>A charge made for more than is due now (a part payment came in meanwhile) settles the invoice and credits the surplus.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Apply_SuccessAfterAPartPayment_SettlesAndCreditsTheSurplus()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var (clientId, invoiceId, claim) = await OpenClaimAsync(scenario);
        await scenario.AddPaymentAsync(invoiceId, 3m);
        var before = await BillingTestSeeds.CreditBalanceOfAsync(scenario.Host, clientId);

        var result = await scenario.InvokeAsync<Result<InvoiceChargeDto>>(
            new ApplyStoredChargeOutcomeCommand(claim.Id, StoredChargeOutcome.Succeeded, "txn-surplus", AmountMinor, Usd, null));

        result.Value!.Status.Should().Be(InvoicePaymentClaimStatus.Settled);
        (await scenario.InvoiceAsync(invoiceId)).IsPaid.Should().BeTrue();
        (await BillingTestSeeds.CreditBalanceOfAsync(scenario.Host, clientId)).Should().Be(before + 3m);
    }

    /// <summary>A decline ends the claim as declined and leaves the invoice unpaid.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Apply_Declined_EndsTheClaim()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var (_, invoiceId, claim) = await OpenClaimAsync(scenario);

        var result = await scenario.InvokeAsync<Result<InvoiceChargeDto>>(
            new ApplyStoredChargeOutcomeCommand(claim.Id, StoredChargeOutcome.Declined, null, null, null, null));

        result.Value!.Status.Should().Be(InvoicePaymentClaimStatus.Declined);
        (await scenario.InvoiceAsync(invoiceId)).IsPaid.Should().BeFalse();
    }

    /// <summary>An action required outcome keeps the claim unresolved and carries the address.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Apply_ActionRequired_KeepsTheAddress()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var (_, _, claim) = await OpenClaimAsync(scenario);

        var result = await scenario.InvokeAsync<Result<InvoiceChargeDto>>(
            new ApplyStoredChargeOutcomeCommand(claim.Id, StoredChargeOutcome.ActionRequired, null, null, null, "https://bank.example/3ds"));

        result.Value!.Status.Should().Be(InvoicePaymentClaimStatus.ActionRequired);
        result.Value.ActionUrl.Should().Be("https://bank.example/3ds");
    }

    /// <summary>No outcome at all means the key lookup found no charge, which ends the claim as abandoned.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Apply_NoOutcome_AbandonsTheClaim()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var (_, _, claim) = await OpenClaimAsync(scenario);

        var result = await scenario.InvokeAsync<Result<InvoiceChargeDto>>(
            new ApplyStoredChargeOutcomeCommand(claim.Id, null, null, null, null, null));

        result.Value!.Status.Should().Be(InvoicePaymentClaimStatus.Abandoned);
    }

    /// <summary>An id that names no claim answers <c>INVOICE_PAYMENT_CLAIM_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Apply_UnknownClaim_IsNotFound()
    {
        using var scenario = new StoredCredentialScenario(factory);

        var result = await scenario.InvokeAsync<Result<InvoiceChargeDto>>(
            new ApplyStoredChargeOutcomeCommand(int.MaxValue, StoredChargeOutcome.Declined, null, null, null, null));

        result.ErrorCode.Should().Be(HostpanelErrorCodes.INVOICE_PAYMENT_CLAIM_NOT_FOUND);
    }
}
