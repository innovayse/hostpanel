using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Hostpanel.Sdk.Plugins.StoredCredentials;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// Completing a hosted-gateway payment that asked to keep the payment method (<c>CompleteGatewayPaymentCommand</c>,
/// reached through <c>POST /api/me/invoices/{id}/gateway-payment/complete</c>; the reconciliation job dispatches the same
/// command) against Postgres: the method the gateway reports is bound to the consent recorded at the start, in the save
/// that settles the invoice, and whatever goes wrong with the binding never fails the payment.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CompleteGatewayPaymentSaveMethodTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The method the fake gateway stores.</summary>
    private static readonly SavedMethod StoredMethod = new("pm-saved-1", "visa", "4242", 12, 2031, false);

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

    /// <summary>
    /// A paid status that carries the stored method binds it to the consent of the session in the save that settles the
    /// invoice, and the method then lists among the client's saved methods.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Complete_PaidWithASavedMethod_BindsItToTheConsent()
    {
        using var scenario = new SavingGatewayScenario(factory);
        var (signedIn, invoiceId) = await scenario.SeedClientWithInvoiceAsync("save-bind");
        var started = await StartSavingAsync(scenario, signedIn, invoiceId);
        scenario.Plugin.MethodOnStatus = StoredMethod;

        var response = await SavingGatewayScenario.CompleteAsync(signedIn.Client, invoiceId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await scenario.InvoiceAsync(invoiceId)).IsPaid.Should().BeTrue();
        var consent = (await scenario.ConsentsOfAsync(signedIn.ClientId)).Single(c => c.Id == started.Id);
        consent.MethodRef.Should().Be(StoredMethod.MethodRef);
        consent.IsActive.Should().BeTrue();

        var listed = await signedIn.Client.GetFromJsonAsync<JsonElement>("/api/me/stored-payment-methods");
        var item = listed.EnumerateArray().Single(e => e.GetProperty("consentId").GetInt32() == started.Id);
        item.GetProperty("detailsAvailable").GetBoolean().Should().BeTrue();
        item.GetProperty("last4").GetString().Should().Be("4242");
    }

    /// <summary>Completing the same payment twice binds the method once and leaves the consent as the first completion made it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Complete_Twice_BindsTheMethodOnce()
    {
        using var scenario = new SavingGatewayScenario(factory);
        var (signedIn, invoiceId) = await scenario.SeedClientWithInvoiceAsync("save-twice-bind");
        await StartSavingAsync(scenario, signedIn, invoiceId);
        scenario.Plugin.MethodOnStatus = StoredMethod;

        (await SavingGatewayScenario.CompleteAsync(signedIn.Client, invoiceId)).StatusCode.Should().Be(HttpStatusCode.OK);
        var second = await SavingGatewayScenario.CompleteAsync(signedIn.Client, invoiceId);

        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var consents = await scenario.ConsentsOfAsync(signedIn.ClientId);
        var consent = consents.Should().ContainSingle().Subject;
        consent.MethodRef.Should().Be(StoredMethod.MethodRef);
        consent.IsRevoked.Should().BeFalse();
        (await scenario.InvoiceAsync(invoiceId)).Transactions.Should().ContainSingle("the payment is on the ledger once");
    }

    /// <summary>
    /// A paid status with no stored method (the gateway did not store it, or the plugin cannot) settles the invoice and
    /// leaves the consent without a method; the payment is never failed for it.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Complete_PaidWithoutASavedMethod_LeavesTheConsentUnattached()
    {
        using var scenario = new SavingGatewayScenario(factory);
        var (signedIn, invoiceId) = await scenario.SeedClientWithInvoiceAsync("save-nomethod");
        await StartSavingAsync(scenario, signedIn, invoiceId);
        scenario.Plugin.MethodOnStatus = null;

        var response = await SavingGatewayScenario.CompleteAsync(signedIn.Client, invoiceId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await scenario.InvoiceAsync(invoiceId)).IsPaid.Should().BeTrue();
        var consent = (await scenario.ConsentsOfAsync(signedIn.ClientId)).Should().ContainSingle().Subject;
        consent.MethodRef.Should().BeNull();
        consent.IsActive.Should().BeFalse();
    }

    /// <summary>A consent the client revoked before the payment finished is not bound; the invoice is still paid.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Complete_AfterTheConsentWasRevoked_BindsNothingAndStillSettles()
    {
        using var scenario = new SavingGatewayScenario(factory);
        var (signedIn, invoiceId) = await scenario.SeedClientWithInvoiceAsync("save-revoked");
        var started = await StartSavingAsync(scenario, signedIn, invoiceId);
        await scenario.RevokeConsentAsync(started.Id);
        scenario.Plugin.MethodOnStatus = StoredMethod;

        var response = await SavingGatewayScenario.CompleteAsync(signedIn.Client, invoiceId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await scenario.InvoiceAsync(invoiceId)).IsPaid.Should().BeTrue();
        var consent = (await scenario.ConsentsOfAsync(signedIn.ClientId)).Should().ContainSingle().Subject;
        consent.MethodRef.Should().BeNull("a revoked consent never takes a method");
        consent.IsRevoked.Should().BeTrue();
    }

    /// <summary>A consent that already covers another method keeps it; the payment is still settled.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Complete_WhenTheConsentCoversAnotherMethod_KeepsItAndStillSettles()
    {
        using var scenario = new SavingGatewayScenario(factory);
        var (signedIn, invoiceId) = await scenario.SeedClientWithInvoiceAsync("save-conflict");
        var started = await StartSavingAsync(scenario, signedIn, invoiceId);
        await scenario.AttachMethodAsync(started.Id, "pm-other");
        scenario.Plugin.MethodOnStatus = StoredMethod;

        var response = await SavingGatewayScenario.CompleteAsync(signedIn.Client, invoiceId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await scenario.InvoiceAsync(invoiceId)).IsPaid.Should().BeTrue();
        (await scenario.ConsentsOfAsync(signedIn.ClientId)).Should().ContainSingle().Which.MethodRef.Should().Be("pm-other");
    }

    /// <summary>
    /// Saving a method the client already holds under an older consent revokes the older consent in the same save and hands
    /// it the default flag, so one method is covered by one consent.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Complete_WithAMethodAlreadyHeld_RevokesTheOlderConsentAndMovesTheDefault()
    {
        using var scenario = new SavingGatewayScenario(factory);
        var (signedIn, firstInvoiceId) = await scenario.SeedClientWithInvoiceAsync("save-older");
        var admin = await BillingTestSeeds.AdminClientAsync(factory, scenario.Host);
        var secondInvoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, signedIn.ClientId, 15m);
        scenario.Plugin.MethodOnStatus = StoredMethod;
        var older = await StartSavingAsync(scenario, signedIn, firstInvoiceId);
        (await SavingGatewayScenario.CompleteAsync(signedIn.Client, firstInvoiceId)).StatusCode.Should().Be(HttpStatusCode.OK);
        await scenario.MakeDefaultAsync(older.Id);
        var newer = await StartSavingAsync(scenario, signedIn, secondInvoiceId);

        var response = await SavingGatewayScenario.CompleteAsync(signedIn.Client, secondInvoiceId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var consents = await scenario.ConsentsOfAsync(signedIn.ClientId);
        var olderNow = consents.Single(c => c.Id == older.Id);
        var newerNow = consents.Single(c => c.Id == newer.Id);
        olderNow.IsRevoked.Should().BeTrue();
        olderNow.IsDefault.Should().BeFalse();
        newerNow.IsActive.Should().BeTrue();
        newerNow.MethodRef.Should().Be(StoredMethod.MethodRef);
        newerNow.IsDefault.Should().BeTrue("the default flag moves to the consent that now covers the method");
    }

    /// <summary>A payment started without the flag binds nothing even when the gateway happens to report a method.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Complete_OfAPaymentThatDidNotAskToSave_BindsNothing()
    {
        using var scenario = new SavingGatewayScenario(factory);
        var (signedIn, invoiceId) = await scenario.SeedClientWithInvoiceAsync("save-notasked");
        (await scenario.StartAsync(signedIn.Client, invoiceId, savePaymentMethod: false, consentTextVersion: null))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        scenario.Plugin.MethodOnStatus = StoredMethod;

        var response = await SavingGatewayScenario.CompleteAsync(signedIn.Client, invoiceId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await scenario.InvoiceAsync(invoiceId)).IsPaid.Should().BeTrue();
        (await scenario.ConsentsOfAsync(signedIn.ClientId)).Should().BeEmpty();
    }
}
