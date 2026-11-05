using Innovayse.Hostpanel.Application.Billing.Commands.ChargeClientRenewalInvoices;
using Innovayse.Hostpanel.Application.Billing.Commands.ChargeInvoiceStoredMethod;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <see cref="ChargeInvoiceStoredMethodCommand"/> against Postgres when a second charge of the same invoice runs at
/// the same time: whichever order the two take, the invoice ends with one blocking claim or one settled claim and the
/// gateway is charged once.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ChargeInvoiceStoredMethodRaceTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// A competing run takes the claim of the invoice between this run's gate check and its claim save: the database
    /// refuses the second blocking claim, this run answers <c>STORED_CHARGE_ALREADY_IN_PROGRESS</c>, the invoice keeps
    /// the one claim that won and this run sent nothing to the gateway.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Charge_CompetingRunTakesTheClaimFirst_EndsInOneClaimAndNoSecondCharge()
    {
        StoredCredentialScenario? scenario = null;
        var clientId = 0;
        var invoiceId = 0;
        var taken = new ClaimTakenInterceptor(() => invoiceId, _ => scenario!.SeedHeldClaimAsync(invoiceId, clientId));
        using var host = new StoredCredentialScenario(factory, saveInterceptor: taken);
        scenario = host;
        clientId = await host.SeedClientAsync();
        await host.SeedConsentAsync(clientId);
        invoiceId = await host.SeedInvoiceAsync(clientId, 10.00m);

        var result = await host.InvokeAsync<Result<InvoiceChargeDto>>(new ChargeInvoiceStoredMethodCommand(invoiceId));

        taken.Fired.Should().BeTrue();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.STORED_CHARGE_ALREADY_IN_PROGRESS);
        (await host.ClaimsOfAsync(invoiceId)).Should().ContainSingle().Which.Status.Should().Be(InvoicePaymentClaimStatus.Held);
        host.State.ChargeCount.Should().Be(0);
        (await host.InvoiceAsync(invoiceId)).IsPaid.Should().BeFalse();
    }

    /// <summary>
    /// A competing run charges and settles the whole invoice between this run's read and its claim save. The first run
    /// has already passed its gate, so its claim is saved; it then reads the invoice again, finds nothing due, ends the
    /// claim as never charged and answers <c>INVOICE_NOT_PAYABLE</c>. The gateway is charged once.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Charge_CompetingRunSettlesTheInvoiceFirst_ChargesOnce()
    {
        StoredCredentialScenario? scenario = null;
        var invoiceId = 0;
        Result<InvoiceChargeDto>? competing = null;
        var taken = new ClaimTakenInterceptor(() => invoiceId, async _ =>
            competing = await scenario!.InvokeAsync<Result<InvoiceChargeDto>>(new ChargeInvoiceStoredMethodCommand(invoiceId)));
        using var host = new StoredCredentialScenario(factory, saveInterceptor: taken);
        scenario = host;
        var clientId = await host.SeedClientAsync();
        await host.SeedConsentAsync(clientId);
        invoiceId = await host.SeedInvoiceAsync(clientId, 10.00m);

        var result = await host.InvokeAsync<Result<InvoiceChargeDto>>(new ChargeInvoiceStoredMethodCommand(invoiceId));

        taken.Fired.Should().BeTrue();
        competing!.IsSuccess.Should().BeTrue();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.INVOICE_NOT_PAYABLE);
        host.State.ChargeCount.Should().Be(1);
        var claims = await host.ClaimsOfAsync(invoiceId);
        claims.Should().ContainSingle(c => c.Status == InvoicePaymentClaimStatus.Settled);
        claims.Should().OnlyContain(c => c.Status == InvoicePaymentClaimStatus.Settled || c.Status == InvoicePaymentClaimStatus.Abandoned);
        var invoice = await host.InvoiceAsync(invoiceId);
        invoice.IsPaid.Should().BeTrue();
        invoice.Transactions.Should().ContainSingle(t => t.Gateway == host.Module);
    }

    /// <summary>
    /// Two charges of one invoice started together: whatever order they take, the gateway is charged once, the invoice
    /// is paid once and exactly one claim settled. This is a smoke test, the order is up to the scheduler; the
    /// interceptor tests above give the deterministic proof.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Charge_TwoAtOnce_ChargeTheGatewayOnce()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var clientId = await scenario.SeedClientAsync();
        await scenario.SeedConsentAsync(clientId);
        var invoiceId = await scenario.SeedInvoiceAsync(clientId, 10.00m);

        var results = await Task.WhenAll(
            scenario.InvokeAsync<Result<InvoiceChargeDto>>(new ChargeInvoiceStoredMethodCommand(invoiceId)),
            scenario.InvokeAsync<Result<InvoiceChargeDto>>(new ChargeInvoiceStoredMethodCommand(invoiceId)));

        results.Should().ContainSingle(r => r.IsSuccess);
        results.Where(r => r.IsFailure).Should().OnlyContain(r =>
            r.ErrorCode == HostpanelErrorCodes.STORED_CHARGE_ALREADY_IN_PROGRESS || r.ErrorCode == HostpanelErrorCodes.INVOICE_NOT_PAYABLE);
        scenario.State.ChargeCount.Should().Be(1);
        (await scenario.ClaimsOfAsync(invoiceId)).Should().ContainSingle(c => c.Status == InvoicePaymentClaimStatus.Settled);
        var invoice = await scenario.InvoiceAsync(invoiceId);
        invoice.IsPaid.Should().BeTrue();
        invoice.Transactions.Should().ContainSingle(t => t.Gateway == scenario.Module);
    }

    /// <summary>
    /// A part payment lands between the read and the claim save, so the claim carries an amount the invoice no longer
    /// owes. The recheck compares the due amount with the claimed one, ends the claim as never charged, answers
    /// <c>INVOICE_NOT_PAYABLE</c> and the gateway is not charged at all (the client is not charged the stale amount).
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Charge_PartPaymentLandsAfterTheRead_AbandonsTheClaimAndChargesNothing()
    {
        StoredCredentialScenario? scenario = null;
        var invoiceId = 0;
        var taken = new ClaimTakenInterceptor(() => invoiceId, _ => scenario!.AddPaymentAsync(invoiceId, 4.00m));
        using var host = new StoredCredentialScenario(factory, saveInterceptor: taken);
        scenario = host;
        var clientId = await host.SeedClientAsync();
        await host.SeedConsentAsync(clientId);
        invoiceId = await host.SeedInvoiceAsync(clientId, 10.00m);

        var result = await host.InvokeAsync<Result<InvoiceChargeDto>>(new ChargeInvoiceStoredMethodCommand(invoiceId));

        taken.Fired.Should().BeTrue();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.INVOICE_NOT_PAYABLE);
        host.State.ChargeCount.Should().Be(0);
        var claim = (await host.ClaimsOfAsync(invoiceId)).Should().ContainSingle().Subject;
        claim.Status.Should().Be(InvoicePaymentClaimStatus.Abandoned);
        claim.AmountMinor.Should().Be(1000L, "the claim kept the amount of the earlier read");
        (await host.InvoiceAsync(invoiceId)).IsPaid.Should().BeFalse();
    }

    /// <summary>
    /// A claim the charge abandoned before it called the plugin is not a failed attempt: the renewal run charges the
    /// invoice again on its next pass instead of waiting out the retry delay, and the attempt cap does not count it.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RenewalRun_ClaimAbandonedBeforeTheGateway_DoesNotCountAsAnAttempt()
    {
        StoredCredentialScenario? scenario = null;
        var invoiceId = 0;
        var taken = new ClaimTakenInterceptor(() => invoiceId, _ => scenario!.AddPaymentAsync(invoiceId, 1.00m));
        using var host = new StoredCredentialScenario(factory, saveInterceptor: taken);
        scenario = host;
        var clientId = await host.SeedClientAsync();
        await host.SeedConsentAsync(clientId);
        invoiceId = await SeedRenewalInvoiceAsync(host, clientId, 12.02m);

        await host.InvokeAsync<Result<int>>(new ChargeClientRenewalInvoicesCommand(clientId));

        taken.Fired.Should().BeTrue();
        host.State.ChargeCount.Should().Be(0);
        (await host.ClaimsOfAsync(invoiceId)).Should().ContainSingle().Which.Status.Should().Be(InvoicePaymentClaimStatus.Abandoned);

        var second = await host.InvokeAsync<Result<int>>(new ChargeClientRenewalInvoicesCommand(clientId));

        second.Value.Should().Be(1, "an attempt that never reached the gateway neither waits nor uses up the allowed attempts");
        host.State.ChargeCount.Should().Be(1);
        (await host.ClaimsOfAsync(invoiceId)).Should().HaveCount(2);
    }

    /// <summary>Raises an unpaid invoice with one renewal line, the kind the renewal run charges.</summary>
    /// <param name="scenario">The scenario whose host to seed.</param>
    /// <param name="clientId">The client.</param>
    /// <param name="amount">The renewal price in USD.</param>
    /// <returns>The invoice's id.</returns>
    private static async Task<int> SeedRenewalInvoiceAsync(StoredCredentialScenario scenario, int clientId, decimal amount)
    {
        using var scope = scenario.Host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var start = DateTimeOffset.UtcNow.AddDays(10);
        var invoice = Invoice.Create(clientId, DateTimeOffset.UtcNow.AddDays(7), "USD", DateTimeOffset.UtcNow);
        invoice.AddDomainRenewal("Renewal: example.test", amount, 1, start, start.AddYears(1));
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(CancellationToken.None);
        return invoice.Id;
    }
}
