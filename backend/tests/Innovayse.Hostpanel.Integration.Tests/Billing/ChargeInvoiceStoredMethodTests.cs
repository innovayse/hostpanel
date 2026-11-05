using Innovayse.Hostpanel.Application.Billing.Commands.ChargeInvoiceStoredMethod;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <see cref="ChargeInvoiceStoredMethodCommand"/> through the real bus against Postgres, with the stored credential
/// capability served by the demo payment plugin: the claim gate, the consent check, every charge outcome, and the
/// unknown outcome that leaves the claim open.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ChargeInvoiceStoredMethodTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Charging an invoice for an ordinary amount takes the money, settles the invoice and ends the claim as settled.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Charge_OrdinaryAmount_SettlesTheInvoice()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var clientId = await scenario.SeedClientAsync();
        await scenario.SeedConsentAsync(clientId);
        var invoiceId = await scenario.SeedInvoiceAsync(clientId, 10.00m);

        var result = await scenario.InvokeAsync<Result<InvoiceChargeDto>>(new ChargeInvoiceStoredMethodCommand(invoiceId));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(InvoicePaymentClaimStatus.Settled);
        var invoice = await scenario.InvoiceAsync(invoiceId);
        invoice.IsPaid.Should().BeTrue();
        invoice.Transactions.Should().ContainSingle(t => t.Gateway == scenario.Module);
        var claim = (await scenario.ClaimsOfAsync(invoiceId)).Should().ContainSingle().Subject;
        claim.Status.Should().Be(InvoicePaymentClaimStatus.Settled);
        claim.TransactionId.Should().StartWith("demo-charge-inv");
        scenario.State.ChargeCount.Should().Be(1);
    }

    /// <summary>A client with no consent is refused before any claim is written or the plugin is called.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Charge_WithoutConsent_IsRefusedAndWritesNothing()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var clientId = await scenario.SeedClientAsync();
        var invoiceId = await scenario.SeedInvoiceAsync(clientId, 10.00m);

        var result = await scenario.InvokeAsync<Result<InvoiceChargeDto>>(new ChargeInvoiceStoredMethodCommand(invoiceId));

        result.ErrorCode.Should().Be(HostpanelErrorCodes.CARD_CONSENT_REQUIRED);
        (await scenario.ClaimsOfAsync(invoiceId)).Should().BeEmpty();
        scenario.State.ChargeCount.Should().Be(0);
        (await scenario.InvoiceAsync(invoiceId)).IsPaid.Should().BeFalse();
    }

    /// <summary>A revoked consent is final: the method is never charged again.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Charge_AfterTheConsentWasRevoked_IsRefused()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var clientId = await scenario.SeedClientAsync();
        var consentId = await scenario.SeedConsentAsync(clientId);
        await scenario.RevokeConsentAsync(consentId);
        var invoiceId = await scenario.SeedInvoiceAsync(clientId, 10.00m);

        var result = await scenario.InvokeAsync<Result<InvoiceChargeDto>>(new ChargeInvoiceStoredMethodCommand(invoiceId));

        result.ErrorCode.Should().Be(HostpanelErrorCodes.CARD_CONSENT_REQUIRED);
        scenario.State.ChargeCount.Should().Be(0);
    }

    /// <summary>A module that is switched off has no capability, so nothing is charged and the invoice stays unpaid.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Charge_ModuleWithoutCapability_IsRefused()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var clientId = await scenario.SeedClientAsync();
        await scenario.SeedConsentAsync(clientId);
        var invoiceId = await scenario.SeedInvoiceAsync(clientId, 10.00m);
        scenario.Resolver.Unregister(scenario.Module);

        var result = await scenario.InvokeAsync<Result<InvoiceChargeDto>>(new ChargeInvoiceStoredMethodCommand(invoiceId));

        result.ErrorCode.Should().Be(HostpanelErrorCodes.STORED_CREDENTIALS_NOT_SUPPORTED);
        (await scenario.ClaimsOfAsync(invoiceId)).Should().BeEmpty();
    }

    /// <summary>A decline is a final answer: the claim is declined, the invoice stays unpaid and a new attempt may start.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Charge_Declined_EndsTheClaimAndLeavesTheInvoiceUnpaid()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var clientId = await scenario.SeedClientAsync();
        await scenario.SeedConsentAsync(clientId);
        var invoiceId = await scenario.SeedInvoiceAsync(clientId, 10.02m);

        var first = await scenario.InvokeAsync<Result<InvoiceChargeDto>>(new ChargeInvoiceStoredMethodCommand(invoiceId));
        var second = await scenario.InvokeAsync<Result<InvoiceChargeDto>>(new ChargeInvoiceStoredMethodCommand(invoiceId));

        first.Value!.Status.Should().Be(InvoicePaymentClaimStatus.Declined);
        second.Value!.Status.Should().Be(InvoicePaymentClaimStatus.Declined);
        var claims = await scenario.ClaimsOfAsync(invoiceId);
        claims.Should().HaveCount(2, "a declined attempt does not block a new one, which gets its own claim");
        claims.Select(c => c.IdempotencyKey).Distinct().Should().HaveCount(2, "a new attempt after a failure gets a new key");
        (await scenario.InvoiceAsync(invoiceId)).IsPaid.Should().BeFalse();
    }

    /// <summary>An action required charge keeps the claim unresolved, hands back the action address and blocks a second charge.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Charge_ActionRequired_KeepsTheClaimOpenAndBlocksASecondCharge()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var clientId = await scenario.SeedClientAsync();
        await scenario.SeedConsentAsync(clientId);
        var invoiceId = await scenario.SeedInvoiceAsync(clientId, 10.03m);

        var first = await scenario.InvokeAsync<Result<InvoiceChargeDto>>(new ChargeInvoiceStoredMethodCommand(invoiceId));
        var second = await scenario.InvokeAsync<Result<InvoiceChargeDto>>(new ChargeInvoiceStoredMethodCommand(invoiceId));

        first.Value!.Status.Should().Be(InvoicePaymentClaimStatus.ActionRequired);
        first.Value.ActionUrl.Should().EndWith("?demo=action");
        second.ErrorCode.Should().Be(HostpanelErrorCodes.STORED_CHARGE_ALREADY_IN_PROGRESS);
        (await scenario.ClaimsOfAsync(invoiceId)).Should().ContainSingle();
        scenario.State.ChargeCount.Should().Be(1);
    }

    /// <summary>A pending charge is never treated as paid and keeps the invoice claimed.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Charge_Pending_IsNotPaid()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var clientId = await scenario.SeedClientAsync();
        await scenario.SeedConsentAsync(clientId);
        var invoiceId = await scenario.SeedInvoiceAsync(clientId, 10.04m);

        var result = await scenario.InvokeAsync<Result<InvoiceChargeDto>>(new ChargeInvoiceStoredMethodCommand(invoiceId));

        result.Value!.Status.Should().Be(InvoicePaymentClaimStatus.Pending);
        (await scenario.InvoiceAsync(invoiceId)).IsPaid.Should().BeFalse();
        (await scenario.ClaimsOfAsync(invoiceId)).Single().IsUnresolved.Should().BeTrue();
    }

    /// <summary>
    /// A plugin that cannot say what happened leaves the claim open, so the invoice is not charged a second time and
    /// the key lookup can find the money later.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Charge_UnknownOutcome_LeavesTheClaimOpen()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var clientId = await scenario.SeedClientAsync();
        await scenario.SeedConsentAsync(clientId);
        var invoiceId = await scenario.SeedInvoiceAsync(clientId, 10.05m);

        var result = await scenario.InvokeAsync<Result<InvoiceChargeDto>>(new ChargeInvoiceStoredMethodCommand(invoiceId));
        var again = await scenario.InvokeAsync<Result<InvoiceChargeDto>>(new ChargeInvoiceStoredMethodCommand(invoiceId));

        result.ErrorCode.Should().Be(HostpanelErrorCodes.PAYMENT_GATEWAY_SERVICE_UNAVAILABLE);
        again.ErrorCode.Should().Be(HostpanelErrorCodes.STORED_CHARGE_ALREADY_IN_PROGRESS);
        (await scenario.ClaimsOfAsync(invoiceId)).Should().ContainSingle().Which.Status.Should().Be(InvoicePaymentClaimStatus.Open);
        scenario.State.ChargeCount.Should().Be(1, "the money was taken once and the claim gate refused a second charge");
    }

    /// <summary>An invoice that is already paid is not charged.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Charge_PaidInvoice_IsNotPayable()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var clientId = await scenario.SeedClientAsync();
        await scenario.SeedConsentAsync(clientId);
        var invoiceId = await scenario.SeedInvoiceAsync(clientId, 10.00m);
        (await scenario.InvokeAsync<Result<InvoiceChargeDto>>(new ChargeInvoiceStoredMethodCommand(invoiceId))).IsSuccess.Should().BeTrue();

        var again = await scenario.InvokeAsync<Result<InvoiceChargeDto>>(new ChargeInvoiceStoredMethodCommand(invoiceId));

        again.ErrorCode.Should().Be(HostpanelErrorCodes.INVOICE_NOT_PAYABLE);
        scenario.State.ChargeCount.Should().Be(1);
    }

    /// <summary>An unknown invoice answers <c>INVOICE_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Charge_UnknownInvoice_IsNotFound()
    {
        using var scenario = new StoredCredentialScenario(factory);

        var result = await scenario.InvokeAsync<Result<InvoiceChargeDto>>(new ChargeInvoiceStoredMethodCommand(int.MaxValue));

        result.ErrorCode.Should().Be(HostpanelErrorCodes.INVOICE_NOT_FOUND);
    }

    /// <summary>
    /// A held claim (the gateway took money the invoice could not record) is final but keeps blocking: the invoice is
    /// never charged again, and the plugin is not called.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Charge_InvoiceWithAHeldClaim_IsNeverChargedAgain()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var clientId = await scenario.SeedClientAsync();
        await scenario.SeedConsentAsync(clientId);
        var invoiceId = await scenario.SeedInvoiceAsync(clientId, 10.00m);
        await scenario.SeedHeldClaimAsync(invoiceId, clientId);

        var result = await scenario.InvokeAsync<Result<InvoiceChargeDto>>(new ChargeInvoiceStoredMethodCommand(invoiceId));

        result.ErrorCode.Should().Be(HostpanelErrorCodes.STORED_CHARGE_ALREADY_IN_PROGRESS);
        (await scenario.ClaimsOfAsync(invoiceId)).Should().ContainSingle().Which.Status.Should().Be(InvoicePaymentClaimStatus.Held);
        scenario.State.ChargeCount.Should().Be(0);
        (await scenario.InvoiceAsync(invoiceId)).IsPaid.Should().BeFalse();
    }

    /// <summary>
    /// A consent revoked after the charge was picked but before the plugin is called is honoured: nothing is sent to
    /// the gateway and the claim is ended as never charged.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Charge_ConsentRevokedBeforeThePluginIsCalled_AbandonsTheClaimAndChargesNothing()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var clientId = await scenario.SeedClientAsync();
        var consentId = await scenario.SeedConsentAsync(clientId);
        var invoiceId = await scenario.SeedInvoiceAsync(clientId, 10.00m);
        scenario.Resolver.OnResolve = _ => scenario.RevokeConsentAsync(consentId);

        var result = await scenario.InvokeAsync<Result<InvoiceChargeDto>>(new ChargeInvoiceStoredMethodCommand(invoiceId));

        result.ErrorCode.Should().Be(HostpanelErrorCodes.CARD_CONSENT_REQUIRED);
        scenario.State.ChargeCount.Should().Be(0);
        (await scenario.ClaimsOfAsync(invoiceId)).Should().ContainSingle().Which.Status.Should().Be(InvoicePaymentClaimStatus.Abandoned);
        (await scenario.InvoiceAsync(invoiceId)).IsPaid.Should().BeFalse();
    }

    /// <summary>The client's default method is charged even when a newer consent exists.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Charge_PrefersTheDefaultConsentOverTheNewest()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var clientId = await scenario.SeedClientAsync();
        var older = await scenario.SeedConsentAsync(clientId, "demo-pm-older");
        await scenario.SeedConsentAsync(clientId, "demo-pm-newer");
        await scenario.MakeDefaultAsync(older);
        var invoiceId = await scenario.SeedInvoiceAsync(clientId, 10.00m);

        var result = await scenario.InvokeAsync<Result<InvoiceChargeDto>>(new ChargeInvoiceStoredMethodCommand(invoiceId));

        result.IsSuccess.Should().BeTrue();
        (await scenario.ClaimsOfAsync(invoiceId)).Should().ContainSingle().Which.MethodRef.Should().Be("demo-pm-older");
    }

    /// <summary>With no default chosen the newest consent is charged.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Charge_WithoutADefault_ChargesTheNewestConsent()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var clientId = await scenario.SeedClientAsync();
        await scenario.SeedConsentAsync(clientId, "demo-pm-older");
        await scenario.SeedConsentAsync(clientId, "demo-pm-newer");
        var invoiceId = await scenario.SeedInvoiceAsync(clientId, 10.00m);

        var result = await scenario.InvokeAsync<Result<InvoiceChargeDto>>(new ChargeInvoiceStoredMethodCommand(invoiceId));

        result.IsSuccess.Should().BeTrue();
        (await scenario.ClaimsOfAsync(invoiceId)).Should().ContainSingle().Which.MethodRef.Should().Be("demo-pm-newer");
    }
}
