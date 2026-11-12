using System.Net;
using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.StoredCredentials;
using Innovayse.Hostpanel.Application.Billing.Commands.CompleteGatewayPayment;
using Innovayse.Hostpanel.Application.Billing.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// The consent recorded when a hosted payment that keeps the payer's method is started ends with the payment when that
/// payment will never store a method (<c>UnboundConsentRevoker</c>): the session was declined, was replaced by a new
/// attempt, or its invoice was settled another way. A session still waiting for the payer keeps its consent.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UnboundConsentRevocationTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Starts a payment that keeps the method and returns the consent it recorded.</summary>
    /// <param name="scenario">The scenario whose module to pay at.</param>
    /// <param name="signedIn">The invoice's own client.</param>
    /// <param name="invoiceId">The invoice.</param>
    /// <returns>The consent recorded for the registered payment.</returns>
    private static async Task<CardChargeConsent> StartSavingAsync(SavingGatewayScenario scenario, SignedInClient signedIn, int invoiceId)
    {
        var started = await scenario.StartAsync(signedIn.Client, invoiceId, savePaymentMethod: true);
        started.StatusCode.Should().Be(HttpStatusCode.OK);
        var gatewayOrderId = (await scenario.InvoiceAsync(invoiceId)).GatewayOrderId;
        return (await scenario.ConsentsOfAsync(signedIn.ClientId)).Single(c => c.SessionId == gatewayOrderId);
    }

    /// <summary>Reads one consent as stored.</summary>
    /// <param name="scenario">The scenario whose module the consent belongs to.</param>
    /// <param name="clientId">The consent's client.</param>
    /// <param name="consentId">The consent.</param>
    /// <returns>The consent.</returns>
    private static async Task<CardChargeConsent> ConsentAsync(SavingGatewayScenario scenario, int clientId, int consentId) =>
        (await scenario.ConsentsOfAsync(clientId)).Single(c => c.Id == consentId);

    /// <summary>A session the gateway declined will never store the method, so the consent recorded for it is revoked and the invoice stays unpaid.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Complete_DeclinedSession_RevokesTheConsent()
    {
        using var scenario = new SavingGatewayScenario(factory);
        var (signedIn, invoiceId) = await scenario.SeedClientWithInvoiceAsync("revoke-declined");
        var consent = await StartSavingAsync(scenario, signedIn, invoiceId);
        scenario.Plugin.StateOnStatus = GatewayPaymentState.Declined;

        var response = await SavingGatewayScenario.CompleteAsync(signedIn.Client, invoiceId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await scenario.InvoiceAsync(invoiceId)).IsPaid.Should().BeFalse();
        var stored = await ConsentAsync(scenario, signedIn.ClientId, consent.Id);
        stored.IsRevoked.Should().BeTrue();
        stored.MethodRef.Should().BeNull();
    }

    /// <summary>A session still waiting for the payer is not over, so its consent stays open.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Complete_PendingSession_KeepsTheConsent()
    {
        using var scenario = new SavingGatewayScenario(factory);
        var (signedIn, invoiceId) = await scenario.SeedClientWithInvoiceAsync("revoke-pending");
        var consent = await StartSavingAsync(scenario, signedIn, invoiceId);
        scenario.Plugin.StateOnStatus = GatewayPaymentState.Pending;

        var response = await SavingGatewayScenario.CompleteAsync(signedIn.Client, invoiceId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ConsentAsync(scenario, signedIn.ClientId, consent.Id)).IsRevoked.Should().BeFalse();
    }

    /// <summary>
    /// An invoice a staff payment settled while the session was open is not paid through that session, so the consent
    /// recorded for the session is revoked when the session is looked at again, whatever it took.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Complete_InvoiceSettledAnotherWay_RevokesTheConsent()
    {
        using var scenario = new SavingGatewayScenario(factory);
        var (signedIn, invoiceId) = await scenario.SeedClientWithInvoiceAsync("revoke-settled");
        var consent = await StartSavingAsync(scenario, signedIn, invoiceId);
        var admin = await BillingTestSeeds.AdminClientAsync(factory, scenario.Host);
        await BillingTestSeeds.MarkPaidAsync(admin, invoiceId, 10m);
        scenario.Plugin.MethodOnStatus = new SavedMethod("pm-orphan", "visa", "4242", 12, 2031, false);

        var response = await SavingGatewayScenario.CompleteAsync(signedIn.Client, invoiceId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stored = await ConsentAsync(scenario, signedIn.ClientId, consent.Id);
        stored.IsRevoked.Should().BeTrue();
        stored.MethodRef.Should().BeNull("the method the session stored belongs to no payment of the invoice, so it is never bound");
    }

    /// <summary>
    /// A completion that loses its save to a staff payment recorded in between settles nothing itself: the money goes to
    /// the balance and the consent recorded for the session is revoked, the same as when the settlement came first.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Complete_LosingTheRaceToAnotherSettlement_RevokesTheConsent()
    {
        using var scenario = new SavingGatewayScenario(factory);
        var (signedIn, invoiceId) = await scenario.SeedClientWithInvoiceAsync("revoke-race");
        var consent = await StartSavingAsync(scenario, signedIn, invoiceId);

        // The loser's context holds the unpaid row before the staff payment is saved, or its save would meet no newer version.
        using var loserScope = scenario.Host.Services.CreateScope();
        var loser = ActivatorUtilities.CreateInstance<CompleteGatewayPaymentHandler>(loserScope.ServiceProvider);
        var loserContext = loserScope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        _ = await loserContext.Invoices.FirstAsync(i => i.Id == invoiceId);
        var admin = await BillingTestSeeds.AdminClientAsync(factory, scenario.Host);
        await BillingTestSeeds.MarkPaidAsync(admin, invoiceId, 10m);

        var result = await loser.HandleAsync(new CompleteGatewayPaymentCommand(invoiceId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.State.Should().Be(GatewayCompletionState.Paid);
        (await ConsentAsync(scenario, signedIn.ClientId, consent.Id)).IsRevoked.Should().BeTrue();
    }

    /// <summary>
    /// A new attempt on the same invoice replaces the old session, which will never store a method now: the old consent is
    /// revoked in the save that points the invoice at the new session, and the new attempt's own consent, when it keeps the
    /// method, stays open.
    /// </summary>
    /// <param name="saveAgain">Whether the replacing attempt asks to keep the method too.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Start_ReplacingASession_RevokesTheOldConsent(bool saveAgain)
    {
        using var scenario = new SavingGatewayScenario(factory);
        var (signedIn, invoiceId) = await scenario.SeedClientWithInvoiceAsync($"revoke-replace-{saveAgain}");
        var old = await StartSavingAsync(scenario, signedIn, invoiceId);
        await scenario.BackdateSessionAsync(invoiceId, DateTimeOffset.UtcNow.AddHours(-1));

        var second = await scenario.StartAsync(signedIn.Client, invoiceId, saveAgain);

        second.StatusCode.Should().Be(HttpStatusCode.OK);
        (await scenario.InvoiceAsync(invoiceId)).GatewayOrderId.Should().NotBe(old.SessionId);
        (await ConsentAsync(scenario, signedIn.ClientId, old.Id)).IsRevoked.Should().BeTrue();
        var consents = await scenario.ConsentsOfAsync(signedIn.ClientId);
        consents.Where(c => c.Id != old.Id).Should().HaveCount(saveAgain ? 1 : 0)
            .And.OnlyContain(c => !c.IsRevoked);
    }
}
