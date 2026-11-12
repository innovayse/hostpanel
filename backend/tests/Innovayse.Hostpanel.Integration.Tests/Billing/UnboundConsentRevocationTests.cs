using System.Net;
using System.Net.Http.Json;
using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.StoredCredentials;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// The consent recorded when a hosted payment that keeps the payer's method is started ends with the payment when that
/// payment will never store a method (<c>UnboundConsentRevoker</c>): the session was declined, was replaced by a new
/// attempt, or its invoice was cancelled or settled another way. A session still waiting for the payer keeps its consent.
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
    /// <param name="clientId">The client whose consents are read.</param>
    /// <param name="consentId">The id of the consent to find.</param>
    /// <returns>The stored consent with that id.</returns>
    private static async Task<CardChargeConsent> ConsentAsync(SavingGatewayScenario scenario, int clientId, int consentId) =>
        (await scenario.ConsentsOfAsync(clientId)).Single(c => c.Id == consentId);

    /// <summary>Waits for the handler of the real payment event to revoke a consent, reading it again until it is, for at most thirty seconds.</summary>
    /// <param name="scenario">The scenario whose module the consent belongs to.</param>
    /// <param name="clientId">The consent's client.</param>
    /// <param name="consentId">The consent.</param>
    /// <returns>The consent as last read.</returns>
    private static async Task<CardChargeConsent> WaitForRevocationAsync(SavingGatewayScenario scenario, int clientId, int consentId)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        var consent = await ConsentAsync(scenario, clientId, consentId);
        while (!consent.IsRevoked && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100));
            consent = await ConsentAsync(scenario, clientId, consentId);
        }

        return consent;
    }

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
    /// An invoice a staff payment settled while the session was open: the payment event the settling save raised closes the
    /// consent recorded for the session at settle time, without anyone polling the session, and the method the session
    /// stored is never bound. The test waits for that real event to be handled.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PaymentReceived_ForAnInvoiceSettledAnotherWay_RevokesTheConsent()
    {
        using var scenario = new SavingGatewayScenario(factory);
        var (signedIn, invoiceId) = await scenario.SeedClientWithInvoiceAsync("revoke-settled");
        var consent = await StartSavingAsync(scenario, signedIn, invoiceId);
        var admin = await BillingTestSeeds.AdminClientAsync(factory, scenario.Host);

        await BillingTestSeeds.MarkPaidAsync(admin, invoiceId, 10m);

        var stored = await WaitForRevocationAsync(scenario, signedIn.ClientId, consent.Id);
        stored.IsRevoked.Should().BeTrue("the payment event closes the consent of a session that did not carry the payment");
        stored.MethodRef.Should().BeNull();
    }

    /// <summary>
    /// An event that reaches the handler late, for an invoice that is not settled any more (marked unpaid since, with a new
    /// session), leaves the consent of the live session alone.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PaymentReceived_ForAnInvoiceThatIsNotSettled_KeepsTheConsent()
    {
        using var scenario = new SavingGatewayScenario(factory);
        var (signedIn, invoiceId) = await scenario.SeedClientWithInvoiceAsync("revoke-stale");
        var consent = await StartSavingAsync(scenario, signedIn, invoiceId);

        await PublishPaymentReceivedAsync(scenario, invoiceId, signedIn.ClientId);

        (await ConsentAsync(scenario, signedIn.ClientId, consent.Id)).IsRevoked.Should().BeFalse();
    }

    /// <summary>The payment of the session itself bound the method in the settling save, so the payment event leaves that consent as it is.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PaymentReceived_ForTheSessionsOwnPayment_KeepsTheBoundConsent()
    {
        using var scenario = new SavingGatewayScenario(factory);
        var (signedIn, invoiceId) = await scenario.SeedClientWithInvoiceAsync("revoke-bound");
        var consent = await StartSavingAsync(scenario, signedIn, invoiceId);
        scenario.Plugin.MethodOnStatus = new SavedMethod("pm-bound", "visa", "4242", 12, 2031, false);
        (await SavingGatewayScenario.CompleteAsync(signedIn.Client, invoiceId)).StatusCode.Should().Be(HttpStatusCode.OK);

        await PublishPaymentReceivedAsync(scenario, invoiceId, signedIn.ClientId);

        var stored = await ConsentAsync(scenario, signedIn.ClientId, consent.Id);
        stored.IsActive.Should().BeTrue();
        stored.IsRevoked.Should().BeFalse();
    }

    /// <summary>Cancelling an invoice ends the consent recorded for its hosted session in the cancelling save.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CancelInvoice_RevokesTheConsentOfItsSession()
    {
        using var scenario = new SavingGatewayScenario(factory);
        var (signedIn, invoiceId) = await scenario.SeedClientWithInvoiceAsync("revoke-cancel");
        var consent = await StartSavingAsync(scenario, signedIn, invoiceId);
        var admin = await BillingTestSeeds.AdminClientAsync(factory, scenario.Host);

        var response = await admin.PostAsync($"/api/billing/{invoiceId}/cancel", content: null);

        response.IsSuccessStatusCode.Should().BeTrue();
        (await ConsentAsync(scenario, signedIn.ClientId, consent.Id)).IsRevoked.Should().BeTrue();
    }

    /// <summary>A bulk cancel ends the consent of the session of every invoice it cancels.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task BulkCancel_RevokesTheConsentOfEachSession()
    {
        using var scenario = new SavingGatewayScenario(factory);
        var (signedIn, invoiceId) = await scenario.SeedClientWithInvoiceAsync("revoke-bulk");
        var consent = await StartSavingAsync(scenario, signedIn, invoiceId);
        var admin = await BillingTestSeeds.AdminClientAsync(factory, scenario.Host);

        var response = await admin.PostAsJsonAsync("/api/billing/bulk", new { invoiceIds = new[] { invoiceId }, action = "Cancel" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ConsentAsync(scenario, signedIn.ClientId, consent.Id)).IsRevoked.Should().BeTrue();
    }

    /// <summary>
    /// A declined session whose consent changes under the completion (the client revoked it, or another writer moved it)
    /// does not fail the completion: the lost save is dropped without touching what else the request tracks, the answer is
    /// still the declined state, and the next completion of the session revokes the consent.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Complete_DeclinedSessionWhoseConsentMovesMeanwhile_DropsTheLostSaveAndRevokesOnTheNextCompletion()
    {
        var sessionId = string.Empty;
        var race = new ConsentRaceInterceptor(() => sessionId, async (db, id) =>
            await db.Set<CardChargeConsent>().Where(c => c.Id == id)
                .ExecuteUpdateAsync(set => set.SetProperty(c => c.IsDefault, false)));
        using var scenario = new SavingGatewayScenario(factory, saveInterceptor: race);
        var (signedIn, invoiceId) = await scenario.SeedClientWithInvoiceAsync("revoke-declined-race");
        var consent = await StartSavingAsync(scenario, signedIn, invoiceId);
        sessionId = consent.SessionId;
        scenario.Plugin.StateOnStatus = GatewayPaymentState.Declined;

        var first = await SavingGatewayScenario.CompleteAsync(signedIn.Client, invoiceId);

        race.Fired.Should().BeTrue();
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ConsentAsync(scenario, signedIn.ClientId, consent.Id)).IsRevoked.Should().BeFalse("the lost save revoked nothing");
        var second = await SavingGatewayScenario.CompleteAsync(signedIn.Client, invoiceId);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ConsentAsync(scenario, signedIn.ClientId, consent.Id)).IsRevoked.Should().BeTrue();
    }

    /// <summary>Publishes the payment event of an invoice and waits until every handler of it has finished.</summary>
    /// <param name="scenario">The scenario whose host handles it.</param>
    /// <param name="invoiceId">The paid invoice.</param>
    /// <param name="clientId">The invoice's client.</param>
    /// <returns>A task that completes once the handlers finished.</returns>
    private static async Task PublishPaymentReceivedAsync(SavingGatewayScenario scenario, int invoiceId, int clientId)
    {
        using var scope = scenario.Host.Services.CreateScope();
        await scope.ServiceProvider.PublishAndWaitAsync(
            new PaymentReceivedEvent(invoiceId, clientId, 10m, "settled-elsewhere") { OccurredAt = DateTimeOffset.UtcNow });
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
