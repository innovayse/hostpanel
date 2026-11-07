using Innovayse.Hostpanel.Application.Billing.Commands.ChargeInvoiceStoredMethod;
using Innovayse.Hostpanel.Application.Billing.Commands.ReconcileInvoicePaymentClaims;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <see cref="ReconcileInvoicePaymentClaimsCommand"/> through the real bus against Postgres: a claim whose answer was
/// lost is resolved by the key lookup, with none of the preconditions a new charge needs.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ReconcileInvoicePaymentClaimsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Seeds a consenting client and an invoice, and charges it.</summary>
    /// <param name="scenario">The scenario to seed.</param>
    /// <param name="amount">The invoice total; its last digits pick the demo outcome.</param>
    /// <returns>The client, the consent, the invoice and the claim the charge wrote.</returns>
    private static async Task<(int ClientId, int ConsentId, int InvoiceId, int ClaimId)> ChargeAsync(
        StoredCredentialScenario scenario, decimal amount)
    {
        var clientId = await scenario.SeedClientAsync();
        var consentId = await scenario.SeedConsentAsync(clientId);
        var invoiceId = await scenario.SeedInvoiceAsync(clientId, amount);
        await scenario.InvokeAsync<Result<InvoiceChargeDto>>(new ChargeInvoiceStoredMethodCommand(invoiceId));
        var claim = (await scenario.ClaimsOfAsync(invoiceId)).Single();
        return (clientId, consentId, invoiceId, claim.Id);
    }

    /// <summary>A charge that took the money but timed out is found by its key and recorded, even after the consent was revoked.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Reconcile_ChargeThatTookTheMoney_IsRecordedEvenAfterTheConsentWasRevoked()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var (_, consentId, invoiceId, claimId) = await ChargeAsync(scenario, 10.05m);
        await scenario.RevokeConsentAsync(consentId);
        await scenario.AgeClaimAsync(claimId);

        var result = await scenario.InvokeAsync<Result<int>>(new ReconcileInvoicePaymentClaimsCommand());

        result.IsSuccess.Should().BeTrue();
        (await scenario.InvoiceAsync(invoiceId)).IsPaid.Should().BeTrue();
        (await scenario.ClaimsOfAsync(invoiceId)).Single().Status.Should().Be(InvoicePaymentClaimStatus.Settled);
        scenario.State.ChargeCount.Should().Be(1, "reconciling only looks the key up and never charges");
    }

    /// <summary>A key the gateway never charged under ends the claim as abandoned, so a new attempt may start.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Reconcile_ChargeThatNeverReachedTheGateway_AbandonsTheClaimAndAllowsANewAttempt()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var (_, _, invoiceId, claimId) = await ChargeAsync(scenario, 10.06m);
        await scenario.AgeClaimAsync(claimId);

        await scenario.InvokeAsync<Result<int>>(new ReconcileInvoicePaymentClaimsCommand());

        (await scenario.ClaimsOfAsync(invoiceId)).Single().Status.Should().Be(InvoicePaymentClaimStatus.Abandoned);
        var again = await scenario.InvokeAsync<Result<InvoiceChargeDto>>(new ChargeInvoiceStoredMethodCommand(invoiceId));
        again.ErrorCode.Should().NotBe(HostpanelErrorCodes.STORED_CHARGE_ALREADY_IN_PROGRESS);
    }

    /// <summary>A pending charge stays unresolved after the lookup, and the pass notes when it looked.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Reconcile_PendingCharge_StaysUnresolvedAndIsStamped()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var (_, _, invoiceId, claimId) = await ChargeAsync(scenario, 10.04m);
        await scenario.AgeClaimAsync(claimId);

        await scenario.InvokeAsync<Result<int>>(new ReconcileInvoicePaymentClaimsCommand());

        var claim = (await scenario.ClaimsOfAsync(invoiceId)).Single();
        claim.Status.Should().Be(InvoicePaymentClaimStatus.Pending);
        claim.IsStale(DateTimeOffset.UtcNow).Should().BeFalse("the pass just looked at it, so it waits before the next lookup");
        (await scenario.InvoiceAsync(invoiceId)).IsPaid.Should().BeFalse();
    }

    /// <summary>A claim that is not stale yet is left alone: the charge may still be in flight.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Reconcile_YoungClaim_IsLeftAlone()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var (_, _, invoiceId, _) = await ChargeAsync(scenario, 10.06m);

        await scenario.InvokeAsync<Result<int>>(new ReconcileInvoicePaymentClaimsCommand());

        (await scenario.ClaimsOfAsync(invoiceId)).Single().Status.Should().Be(InvoicePaymentClaimStatus.Open);
    }

    /// <summary>A claim whose module was switched off is not abandoned: a lookup that was not made decides nothing.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Reconcile_ModuleSwitchedOff_LeavesTheClaimUnresolved()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var (_, _, invoiceId, claimId) = await ChargeAsync(scenario, 10.06m);
        await scenario.AgeClaimAsync(claimId);
        scenario.Resolver.Unregister(scenario.Module);

        await scenario.InvokeAsync<Result<int>>(new ReconcileInvoicePaymentClaimsCommand());

        (await scenario.ClaimsOfAsync(invoiceId)).Single().Status.Should().Be(InvoicePaymentClaimStatus.Open);
    }
}
