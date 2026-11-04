using Innovayse.Hostpanel.Application.Billing.Commands.ChargeClientRenewalInvoices;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <see cref="ChargeClientRenewalInvoicesCommand"/> through the real bus against Postgres: a client with a consent has
/// the open renewal invoices charged, and everyone else is left to the usual invoice and pay link flow. The charges
/// here are declined (an amount ending in 02) so no payment event follows and the test looks at the charge alone.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ChargeClientRenewalInvoicesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Raises an unpaid invoice with one renewal line.</summary>
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

    /// <summary>A client with a consent has the open renewal invoice charged through the capability.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Charge_ClientWithConsent_ChargesTheRenewalInvoice()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var clientId = await scenario.SeedClientAsync();
        await scenario.SeedConsentAsync(clientId);
        var invoiceId = await SeedRenewalInvoiceAsync(scenario, clientId, 12.02m);

        var result = await scenario.InvokeAsync<Result<int>>(new ChargeClientRenewalInvoicesCommand(clientId));

        result.Value.Should().Be(1);
        (await scenario.ClaimsOfAsync(invoiceId)).Should().ContainSingle().Which.Status.Should().Be(InvoicePaymentClaimStatus.Declined);
        scenario.State.ChargeCount.Should().Be(1);
    }

    /// <summary>A client with no consent is skipped: no claim, no plugin call, and the invoice goes the usual way.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Charge_ClientWithoutConsent_DoesNothing()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var clientId = await scenario.SeedClientAsync();
        var invoiceId = await SeedRenewalInvoiceAsync(scenario, clientId, 12.02m);

        var result = await scenario.InvokeAsync<Result<int>>(new ChargeClientRenewalInvoicesCommand(clientId));

        result.Value.Should().Be(0);
        (await scenario.ClaimsOfAsync(invoiceId)).Should().BeEmpty();
        scenario.State.ChargeCount.Should().Be(0);
    }

    /// <summary>An invoice that is not a renewal is not charged by the renewal run.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Charge_InvoiceWithoutRenewalLine_IsLeftAlone()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var clientId = await scenario.SeedClientAsync();
        await scenario.SeedConsentAsync(clientId);
        var invoiceId = await scenario.SeedInvoiceAsync(clientId, 12.02m);

        var result = await scenario.InvokeAsync<Result<int>>(new ChargeClientRenewalInvoicesCommand(clientId));

        result.Value.Should().Be(0);
        (await scenario.ClaimsOfAsync(invoiceId)).Should().BeEmpty();
    }

    /// <summary>An invoice that already has a charge in flight is not charged again by the next renewal run.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Charge_InvoiceWithAChargeInFlight_IsNotChargedAgain()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var clientId = await scenario.SeedClientAsync();
        await scenario.SeedConsentAsync(clientId);
        var invoiceId = await SeedRenewalInvoiceAsync(scenario, clientId, 12.03m);
        await scenario.InvokeAsync<Result<int>>(new ChargeClientRenewalInvoicesCommand(clientId));

        var second = await scenario.InvokeAsync<Result<int>>(new ChargeClientRenewalInvoicesCommand(clientId));

        second.Value.Should().Be(0);
        (await scenario.ClaimsOfAsync(invoiceId)).Should().ContainSingle().Which.Status.Should().Be(InvoicePaymentClaimStatus.ActionRequired);
        scenario.State.ChargeCount.Should().Be(1);
    }

    /// <summary>A held claim stops the renewal run from charging the invoice again.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Charge_InvoiceWithAHeldClaim_IsLeftAlone()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var clientId = await scenario.SeedClientAsync();
        await scenario.SeedConsentAsync(clientId);
        var invoiceId = await SeedRenewalInvoiceAsync(scenario, clientId, 12.00m);
        await scenario.SeedHeldClaimAsync(invoiceId, clientId);

        var result = await scenario.InvokeAsync<Result<int>>(new ChargeClientRenewalInvoicesCommand(clientId));

        result.Value.Should().Be(0);
        scenario.State.ChargeCount.Should().Be(0);
        (await scenario.ClaimsOfAsync(invoiceId)).Should().ContainSingle();
    }

    /// <summary>A declined invoice is not charged again on the next pass: the retry wait has not passed.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Charge_RightAfterADecline_WaitsForTheBackOff()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var clientId = await scenario.SeedClientAsync();
        await scenario.SeedConsentAsync(clientId);
        var invoiceId = await SeedRenewalInvoiceAsync(scenario, clientId, 12.02m);
        await scenario.InvokeAsync<Result<int>>(new ChargeClientRenewalInvoicesCommand(clientId));

        var second = await scenario.InvokeAsync<Result<int>>(new ChargeClientRenewalInvoicesCommand(clientId));

        second.Value.Should().Be(0);
        (await scenario.ClaimsOfAsync(invoiceId)).Should().ContainSingle();
        scenario.State.ChargeCount.Should().Be(1);
    }

    /// <summary>Once the retry wait has passed a declined invoice is charged again, with a claim and a key of its own.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Charge_AfterTheBackOff_RetriesTheInvoice()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var clientId = await scenario.SeedClientAsync();
        await scenario.SeedConsentAsync(clientId);
        var invoiceId = await SeedRenewalInvoiceAsync(scenario, clientId, 12.02m);
        await scenario.InvokeAsync<Result<int>>(new ChargeClientRenewalInvoicesCommand(clientId));
        await scenario.AgeFailedClaimsAsync(invoiceId, TimeSpan.FromDays(10));

        var second = await scenario.InvokeAsync<Result<int>>(new ChargeClientRenewalInvoicesCommand(clientId));

        second.Value.Should().Be(1);
        (await scenario.ClaimsOfAsync(invoiceId)).Should().HaveCount(2);
        scenario.State.ChargeCount.Should().Be(2);
    }

    /// <summary>After the most attempts an invoice gets, the run leaves it unpaid for the usual flow however long it waits.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Charge_AfterTheMostAttempts_StopsRetrying()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var clientId = await scenario.SeedClientAsync();
        await scenario.SeedConsentAsync(clientId);
        var invoiceId = await SeedRenewalInvoiceAsync(scenario, clientId, 12.02m);
        var allowed = factory.Services.GetRequiredService<IOptions<StoredChargeOptions>>().Value.MaxAttemptsPerInvoice;
        for (var attempt = 0; attempt < allowed; attempt++)
        {
            (await scenario.InvokeAsync<Result<int>>(new ChargeClientRenewalInvoicesCommand(clientId))).Value.Should().Be(1);
            await scenario.AgeFailedClaimsAsync(invoiceId, TimeSpan.FromDays(10));
        }

        var extra = await scenario.InvokeAsync<Result<int>>(new ChargeClientRenewalInvoicesCommand(clientId));

        extra.Value.Should().Be(0);
        (await scenario.ClaimsOfAsync(invoiceId)).Should().HaveCount(allowed);
        scenario.State.ChargeCount.Should().Be(allowed);
    }
}
