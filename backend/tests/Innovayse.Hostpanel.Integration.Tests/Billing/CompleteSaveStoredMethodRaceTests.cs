using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>POST /api/me/stored-payment-methods/sessions/{sessionId}/complete</c> against Postgres when another writer
/// touches the consent while the complete is running: two completes of one session leave one active consent, and a
/// revocation that lands between the read and the save of the activation is never overwritten.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CompleteSaveStoredMethodRaceTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
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

    /// <summary>Reads every consent row written for a session.</summary>
    /// <param name="scenario">The scenario whose module the session belongs to.</param>
    /// <param name="sessionId">The session id.</param>
    /// <returns>The rows.</returns>
    private static async Task<List<CardChargeConsent>> RowsOfAsync(StoredCredentialScenario scenario, string sessionId)
    {
        using var scope = scenario.Host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.CardChargeConsents.AsNoTracking()
            .Where(c => c.GatewayModuleId == scenario.Module && c.SessionId == sessionId)
            .ToListAsync();
    }

    /// <summary>
    /// A competing complete of the same session binds the method between this complete's read and its save: the save
    /// loses on the stale row version and answers 409, the session still has one consent row and it is active, and a
    /// repeat then answers the same consent.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Complete_AnotherCompleteWinsTheRace_LeavesOneActiveConsent()
    {
        StoredCredentialScenario? scenario = null;
        var sessionId = string.Empty;
        var race = new ConsentRaceInterceptor(() => sessionId, async (db, id) =>
        {
            var saved = await scenario!.Plugin.CompleteSaveMethodAsync(sessionId, CancellationToken.None);
            var methodRef = saved.Method!.MethodRef;
            await db.Set<CardChargeConsent>().Where(c => c.Id == id)
                .ExecuteUpdateAsync(set => set.SetProperty(c => c.MethodRef, methodRef));
        });
        using var host = new StoredCredentialScenario(factory, saveInterceptor: race);
        scenario = host;
        var signedIn = await BillingTestSeeds.RegisterClientAsync(factory, host.Host, "complete-race");
        sessionId = await OpenSessionAsync(host, signedIn.Client);

        var loser = await CompleteAsync(host, signedIn.Client, sessionId);

        race.Fired.Should().BeTrue();
        loser.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var row = (await RowsOfAsync(host, sessionId)).Should().ContainSingle().Subject;
        row.IsActive.Should().BeTrue();
        var repeat = await CompleteAsync(host, signedIn.Client, sessionId);
        repeat.StatusCode.Should().Be(HttpStatusCode.OK);
        (await repeat.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("consentId").GetInt32().Should().Be(row.Id);
        (await RowsOfAsync(host, sessionId)).Should().ContainSingle();
    }

    /// <summary>
    /// Two completes of one session sent at the same time never leave a second consent row: each answers 200 or the
    /// 409 of a lost race, at least one wins, and the session ends with one active consent. This is a smoke test, the
    /// order is up to the scheduler; the interceptor tests give the deterministic proof.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Complete_TwoAtOnce_EndInOneActiveConsent()
    {
        using var scenario = new StoredCredentialScenario(factory);
        var signedIn = await BillingTestSeeds.RegisterClientAsync(factory, scenario.Host, "complete-parallel");
        var sessionId = await OpenSessionAsync(scenario, signedIn.Client);

        var responses = await Task.WhenAll(
            CompleteAsync(scenario, signedIn.Client, sessionId),
            CompleteAsync(scenario, signedIn.Client, sessionId));

        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK || r.StatusCode == HttpStatusCode.Conflict);
        responses.Should().Contain(r => r.StatusCode == HttpStatusCode.OK);
        (await RowsOfAsync(scenario, sessionId)).Should().ContainSingle().Which.IsActive.Should().BeTrue();
    }

    /// <summary>
    /// A revocation lands between the read and the save of the activation: the activation loses on the stale row
    /// version (409), and the consent ends revoked with no method bound, never active with a revocation stamped on it.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Complete_RevokedBetweenReadAndSave_EndsRevokedAndNeverActive()
    {
        var sessionId = string.Empty;
        var race = new ConsentRaceInterceptor(() => sessionId, (db, id) => db.Set<CardChargeConsent>()
            .Where(c => c.Id == id)
            .ExecuteUpdateAsync(set => set.SetProperty(c => c.RevokedAt, (DateTimeOffset?)DateTimeOffset.UtcNow)));
        using var scenario = new StoredCredentialScenario(factory, saveInterceptor: race);
        var signedIn = await BillingTestSeeds.RegisterClientAsync(factory, scenario.Host, "complete-revoked-race");
        sessionId = await OpenSessionAsync(scenario, signedIn.Client);

        var response = await CompleteAsync(scenario, signedIn.Client, sessionId);

        race.Fired.Should().BeTrue();
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var row = (await RowsOfAsync(scenario, sessionId)).Should().ContainSingle().Subject;
        row.RevokedAt.Should().NotBeNull();
        row.MethodRef.Should().BeNull("the activation must not overwrite the revocation");
        row.IsActive.Should().BeFalse();
        var after = await CompleteAsync(scenario, signedIn.Client, sessionId);
        (await BillingTestSeeds.CodeOfAsync(after)).Should().Be(HostpanelErrorCodes.CARD_CONSENT_REQUIRED);
    }
}
