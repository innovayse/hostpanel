using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Hostpanel.Sdk.Plugins.StoredCredentials;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>POST /api/me/stored-payment-methods/sessions/{sessionId}/complete</c> against Postgres, with the stored
/// credential capability served by the demo payment plugin: the stored method is bound to the consent recorded when the
/// session opened, completing twice answers the same method, and another client's session is no consent.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CompleteSaveStoredMethodTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Opens a save session as the caller and returns its id.</summary>
    /// <param name="scenario">The scenario whose module to open it at.</param>
    /// <param name="caller">The signed-in client.</param>
    /// <returns>The session id.</returns>
    private static async Task<string> OpenSessionAsync(StoredCredentialScenario scenario, HttpClient caller)
    {
        var begun = await caller.PostAsJsonAsync(
            "/api/me/stored-payment-methods/sessions",
            new { gatewayModuleId = scenario.Module, consentAccepted = true, returnUrl = BillingTestSeeds.AllowedReturnUrl, consentTextVersion = ConsentTextVersionWire.AccountCard });
        begun.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await begun.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("sessionId").GetString()!;
    }

    /// <summary>Completes a session as the caller.</summary>
    /// <param name="scenario">The scenario whose module the session belongs to.</param>
    /// <param name="caller">The signed-in client.</param>
    /// <param name="sessionId">The session id.</param>
    /// <returns>The response.</returns>
    private static Task<HttpResponseMessage> CompleteAsync(StoredCredentialScenario scenario, HttpClient caller, string sessionId) =>
        caller.PostAsJsonAsync($"/api/me/stored-payment-methods/sessions/{sessionId}/complete", new { gatewayModuleId = scenario.Module });

    /// <summary>Completing a session binds the stored method to the consent, which then becomes active.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Complete_AfterTheSessionWasUsed_BindsTheMethodToTheConsent()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var signedIn = await BillingTestSeeds.RegisterClientAsync(factory, scenario.Host, "complete-ok");
        var sessionId = await OpenSessionAsync(scenario, signedIn.Client);

        var response = await CompleteAsync(scenario, signedIn.Client, sessionId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("last4").GetString().Should().Be("4242");
        using var scope = scenario.Host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var consent = await context.CardChargeConsents.AsNoTracking().SingleAsync(c => c.GatewayModuleId == scenario.Module && c.SessionId == sessionId);
        consent.MethodRef.Should().Be(body.GetProperty("methodRef").GetString());
        consent.IsActive.Should().BeTrue();
        body.GetProperty("consentId").GetInt32().Should().Be(consent.Id);
    }

    /// <summary>Completing the same session twice answers the same method and binds nothing twice.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Complete_Twice_AnswersTheSameMethod()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var signedIn = await BillingTestSeeds.RegisterClientAsync(factory, scenario.Host, "complete-twice");
        var sessionId = await OpenSessionAsync(scenario, signedIn.Client);

        var first = await CompleteAsync(scenario, signedIn.Client, sessionId);
        var second = await CompleteAsync(scenario, signedIn.Client, sessionId);

        second.StatusCode.Should().Be(HttpStatusCode.OK);
        (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("methodRef").GetString()
            .Should().Be((await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("methodRef").GetString());
    }

    /// <summary>A session id of another client's is no consent of the caller: 404 and nothing is bound.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Complete_AnotherClientsSession_IsNotFound()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var owner = await BillingTestSeeds.RegisterClientAsync(factory, scenario.Host, "complete-owner");
        var stranger = await BillingTestSeeds.RegisterClientAsync(factory, scenario.Host, "complete-stranger");
        var sessionId = await OpenSessionAsync(scenario, owner.Client);

        var response = await CompleteAsync(scenario, stranger.Client, sessionId);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.CARD_CONSENT_NOT_FOUND);
    }

    /// <summary>A session the payer abandoned (the gateway never stored a method) is refused with <c>STORED_METHOD_NOT_SAVED</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Complete_AbandonedSession_IsNotSaved()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var signedIn = await BillingTestSeeds.RegisterClientAsync(factory, scenario.Host, "complete-abandoned");
        var sessionId = await OpenSessionAsync(scenario, signedIn.Client);
        scenario.State.Clear();

        var response = await CompleteAsync(scenario, signedIn.Client, sessionId);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.STORED_METHOD_NOT_SAVED);
    }

    /// <summary>
    /// A consent that already covers a different method is not rebound: the answer is a 409 with
    /// <c>CARD_CONSENT_ALREADY_ATTACHED</c> instead of an unhandled error, and the consent keeps its method.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Complete_ConsentAlreadyCoversAnotherMethod_IsConflict()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var signedIn = await BillingTestSeeds.RegisterClientAsync(factory, scenario.Host, "complete-attached");
        var sessionId = await OpenSessionAsync(scenario, signedIn.Client);
        using (var scope = scenario.Host.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var consent = await context.CardChargeConsents.SingleAsync(c => c.GatewayModuleId == scenario.Module && c.SessionId == sessionId);
            consent.AttachMethod("some-other-method");
            await context.SaveChangesAsync(CancellationToken.None);
        }

        var response = await CompleteAsync(scenario, signedIn.Client, sessionId);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.CARD_CONSENT_ALREADY_ATTACHED);
        using var check = scenario.Host.Services.CreateScope();
        var checkContext = check.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await checkContext.CardChargeConsents.AsNoTracking().SingleAsync(c => c.GatewayModuleId == scenario.Module && c.SessionId == sessionId)).MethodRef
            .Should().Be("some-other-method");
    }

    /// <summary>
    /// A method whose reference is longer than the consent can hold is answered as not saved instead of a database
    /// error, and the consent stays without a method.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Complete_MethodReferenceTooLongForTheConsent_IsNotSaved()
    {
        using var scenario = new SavingGatewayScenario(factory);
        var signedIn = await BillingTestSeeds.RegisterClientAsync(factory, scenario.Host, "complete-long-ref");
        scenario.Plugin.SaveSessionResult = new SavedMethodResult(
            true, new SavedMethod(new string('m', CardChargeConsent.MaxMethodRefLength + 1), "visa", "4242", 12, 2031, false));
        var begun = await signedIn.Client.PostAsJsonAsync(
            "/api/me/stored-payment-methods/sessions",
            new { gatewayModuleId = scenario.Module, consentAccepted = true, returnUrl = BillingTestSeeds.AllowedReturnUrl, consentTextVersion = ConsentTextVersionWire.AccountCard });
        begun.StatusCode.Should().Be(HttpStatusCode.OK);
        var sessionId = (await begun.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("sessionId").GetString()!;

        var response = await signedIn.Client.PostAsJsonAsync(
            $"/api/me/stored-payment-methods/sessions/{sessionId}/complete", new { gatewayModuleId = scenario.Module });

        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.STORED_METHOD_NOT_SAVED);
        using var scope = scenario.Host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.CardChargeConsents.AsNoTracking().SingleAsync(c => c.GatewayModuleId == scenario.Module && c.SessionId == sessionId)).MethodRef
            .Should().BeNull();
    }

    /// <summary>
    /// Saving the same method again in a new session leaves one active consent for it: the older consent is revoked in
    /// the same save, the newer one keeps the evidence, and the default flag moves to it.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Complete_SameMethodAgain_RevokesTheOlderConsentAndKeepsTheDefault()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var signedIn = await BillingTestSeeds.RegisterClientAsync(factory, scenario.Host, "complete-dedupe");
        var sessionId = await OpenSessionAsync(scenario, signedIn.Client);
        var saved = await scenario.Plugin.CompleteSaveMethodAsync(sessionId, CancellationToken.None);
        var methodRef = saved.Method!.MethodRef;
        int olderId;
        using (var scope = scenario.Host.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var older = CardChargeConsent.Grant(
                signedIn.ClientId, scenario.Module, $"older-{Guid.NewGuid():N}", DateTimeOffset.UtcNow.AddDays(-1));
            older.AttachMethod(methodRef);
            older.SetDefault([]);
            context.CardChargeConsents.Add(older);
            await context.SaveChangesAsync(CancellationToken.None);
            olderId = older.Id;
        }

        var response = await CompleteAsync(scenario, signedIn.Client, sessionId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var consentId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("consentId").GetInt32();
        var olderAfter = await scenario.ConsentAsync(olderId);
        olderAfter.IsRevoked.Should().BeTrue();
        olderAfter.IsDefault.Should().BeFalse();
        var current = await scenario.ConsentAsync(consentId);
        current.IsActive.Should().BeTrue();
        current.IsDefault.Should().BeTrue("the default moved from the consent that was replaced");
        current.ConsentTextVersion.Should().Be(ConsentTextVersionWire.AccountCard);
    }
}
