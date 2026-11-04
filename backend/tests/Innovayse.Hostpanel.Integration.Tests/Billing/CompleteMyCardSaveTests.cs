using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.SDK.Plugins.StoredCredentials;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>POST /api/me/payment-methods/sessions/complete</c> against Postgres with the saved-method capability replaced
/// by <see cref="FakeStoredCredentialPlugin"/>: the gateway's own record decides whether the card was saved, only a
/// card on the client's own account activates the agreement, a withdrawn agreement never comes back, and the race
/// between a withdrawal and a confirmation ends Revoked.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CompleteMyCardSaveTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The route under test.</summary>
    private const string Route = "/api/me/payment-methods/sessions/complete";

    /// <summary>The card the default fake session saves.</summary>
    private const string SavedMethod = "pm_saved_1";

    /// <summary>Registers a client with an account at the fake module and a Pending agreement for a session.</summary>
    /// <param name="gateway">The fake capability host.</param>
    /// <param name="label">Distinguishes the seeded email between tests.</param>
    /// <returns>The client's caller, its id and the session of its Pending agreement.</returns>
    private async Task<(HttpClient Caller, int ClientId, string SessionId)> SeedPendingAsync(StoredCredentialHost gateway, string label)
    {
        var (caller, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, gateway.Host, label);
        await gateway.LinkAsync(clientId, $"cus_{Guid.NewGuid():N}"[..24]);
        var sessionId = await gateway.AddConsentAsync(clientId, methodRef: null);
        return (caller, clientId, sessionId);
    }

    /// <summary>Sends the completion of a session at the fake module.</summary>
    /// <param name="caller">The client.</param>
    /// <param name="sessionId">The session to complete.</param>
    /// <returns>The response.</returns>
    private static Task<HttpResponseMessage> CompleteAsync(HttpClient caller, string sessionId) =>
        caller.PostAsJsonAsync(Route, new { gatewayModuleId = StoredCredentialHost.Module, sessionId });

    /// <summary>A saved card on the client's account activates the agreement for exactly that card, and the first card becomes the default.</summary>
    [Fact]
    public async Task Complete_SavedCard_ActivatesAndMakesTheFirstCardTheDefaultAsync()
    {
        var gateway = StoredCredentialHost.Create(factory);
        gateway.Plugin.Methods.Add(StoredCredentialHost.Card(SavedMethod, isDefault: false));
        var (caller, clientId, sessionId) = await SeedPendingAsync(gateway, "complete");

        var response = await CompleteAsync(caller, sessionId);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var consent = (await gateway.ConsentsOfAsync(clientId)).Should().ContainSingle().Subject;
        consent.Status.Should().Be(CardConsentStatus.Active);
        consent.GatewayMethodRef.Should().Be(SavedMethod);
        consent.ActivatedAt.Should().NotBeNull();
        gateway.Plugin.LastDefaultMethod.Should().Be(SavedMethod);
    }

    /// <summary>A card saved next to an existing default leaves that default alone.</summary>
    [Fact]
    public async Task Complete_AnotherCardIsAlreadyTheDefault_LeavesTheDefaultAsync()
    {
        var gateway = StoredCredentialHost.Create(factory);
        gateway.Plugin.Methods.AddRange([StoredCredentialHost.Card("pm_existing"), StoredCredentialHost.Card(SavedMethod, isDefault: false)]);
        var (caller, _, sessionId) = await SeedPendingAsync(gateway, "complete-has-default");

        var response = await CompleteAsync(caller, sessionId);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        gateway.Plugin.LastDefaultMethod.Should().BeNull();
    }

    /// <summary>Completing the same session again succeeds and changes nothing.</summary>
    [Fact]
    public async Task Complete_SecondCall_IsIdempotentAsync()
    {
        var gateway = StoredCredentialHost.Create(factory);
        gateway.Plugin.Methods.Add(StoredCredentialHost.Card(SavedMethod, isDefault: false));
        var (caller, clientId, sessionId) = await SeedPendingAsync(gateway, "complete-twice");
        await CompleteAsync(caller, sessionId);
        var first = (await gateway.ConsentsOfAsync(clientId)).Single();

        var again = await CompleteAsync(caller, sessionId);

        again.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var after = (await gateway.ConsentsOfAsync(clientId)).Should().ContainSingle().Subject;
        after.Status.Should().Be(CardConsentStatus.Active);
        after.ActivatedAt.Should().Be(first.ActivatedAt);
    }

    /// <summary>A session the client never began is refused with <c>CARD_SETUP_NOT_FOUND</c>.</summary>
    [Fact]
    public async Task Complete_UnknownSession_RefusesNotFoundAsync()
    {
        var gateway = StoredCredentialHost.Create(factory);
        var (caller, _, _) = await SeedPendingAsync(gateway, "complete-unknown");

        var response = await CompleteAsync(caller, "sess_never_begun");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.CARD_SETUP_NOT_FOUND);
    }

    /// <summary>Another client's session is not found for this client, and their agreement stays Pending.</summary>
    [Fact]
    public async Task Complete_AnotherClientsSession_RefusesNotFoundAsync()
    {
        var gateway = StoredCredentialHost.Create(factory);
        gateway.Plugin.Methods.Add(StoredCredentialHost.Card(SavedMethod, isDefault: false));
        var (_, ownerId, sessionId) = await SeedPendingAsync(gateway, "complete-owner");
        var (stranger, _, _) = await SeedPendingAsync(gateway, "complete-stranger");

        var response = await CompleteAsync(stranger, sessionId);

        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.CARD_SETUP_NOT_FOUND);
        (await gateway.ConsentsOfAsync(ownerId)).Single().Status.Should().Be(CardConsentStatus.Pending);
    }

    /// <summary>A gateway that did not save the card refuses with <c>CARD_SETUP_NOT_COMPLETED</c> and the agreement stays Pending.</summary>
    [Fact]
    public async Task Complete_NotSaved_RefusesNotCompletedAsync()
    {
        var gateway = StoredCredentialHost.Create(factory);
        gateway.Plugin.CompletedSession = new SavedMethodResult(false, null);
        var (caller, clientId, sessionId) = await SeedPendingAsync(gateway, "complete-not-saved");

        var response = await CompleteAsync(caller, sessionId);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.CARD_SETUP_NOT_COMPLETED);
        (await gateway.ConsentsOfAsync(clientId)).Single().Status.Should().Be(CardConsentStatus.Pending);
    }

    /// <summary>A card that is not on the client's own account is refused with <c>PAYMENT_METHOD_NOT_OWNED</c> and nothing is activated.</summary>
    [Fact]
    public async Task Complete_MethodNotOnTheCustomersList_RefusesNotOwnedAsync()
    {
        var gateway = StoredCredentialHost.Create(factory);
        gateway.Plugin.Methods.Add(StoredCredentialHost.Card("pm_other_card"));
        var (caller, clientId, sessionId) = await SeedPendingAsync(gateway, "complete-not-owned");

        var response = await CompleteAsync(caller, sessionId);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.PAYMENT_METHOD_NOT_OWNED);
        (await gateway.ConsentsOfAsync(clientId)).Single().Status.Should().Be(CardConsentStatus.Pending);
    }

    /// <summary>An agreement withdrawn before the confirmation stays withdrawn and the confirmation is refused.</summary>
    [Fact]
    public async Task Complete_RevokedBeforeConfirm_StaysRevokedAsync()
    {
        var gateway = StoredCredentialHost.Create(factory);
        gateway.Plugin.Methods.Add(StoredCredentialHost.Card(SavedMethod, isDefault: false));
        var (caller, clientId, sessionId) = await SeedPendingAsync(gateway, "complete-revoked");
        await caller.DeleteAsync("/api/me/payment-methods/auto-charge");

        var response = await CompleteAsync(caller, sessionId);

        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.CARD_SETUP_NOT_COMPLETED);
        (await gateway.ConsentsOfAsync(clientId)).Single().Status.Should().Be(CardConsentStatus.Revoked);
        gateway.Plugin.LastDefaultMethod.Should().BeNull();
    }

    /// <summary>
    /// The agreement is withdrawn between the confirmation's read and its save: the save fails on the stale row
    /// version, the agreement ends Revoked and the confirmation answers <c>CARD_SETUP_NOT_COMPLETED</c>.
    /// </summary>
    [Fact]
    public async Task Complete_RevokedBetweenReadAndSave_EndsRevokedAsync()
    {
        var race = new ConsentRaceInterceptor((db, id) => db.Set<CardChargeConsent>()
            .Where(c => c.Id == id)
            .ExecuteUpdateAsync(set => set.SetProperty(c => c.Status, CardConsentStatus.Revoked)));
        var gateway = StoredCredentialHost.Create(factory, race);
        gateway.Plugin.Methods.Add(StoredCredentialHost.Card(SavedMethod, isDefault: false));
        var (caller, clientId, sessionId) = await SeedPendingAsync(gateway, "complete-race");

        var response = await CompleteAsync(caller, sessionId);

        race.Fired.Should().BeTrue();
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.CARD_SETUP_NOT_COMPLETED);
        (await gateway.ConsentsOfAsync(clientId)).Single().Status.Should().Be(CardConsentStatus.Revoked);
        gateway.Plugin.LastDefaultMethod.Should().BeNull();
    }

    /// <summary>A gateway that cannot be reached answers 503 and the agreement stays Pending.</summary>
    [Fact]
    public async Task Complete_GatewayUnavailable_Answers503Async()
    {
        var gateway = StoredCredentialHost.Create(factory);
        gateway.Plugin.Methods.Add(StoredCredentialHost.Card(SavedMethod, isDefault: false));
        gateway.Plugin.MethodCallsThrow = true;
        var (caller, clientId, sessionId) = await SeedPendingAsync(gateway, "complete-unavailable");

        var response = await CompleteAsync(caller, sessionId);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await gateway.ConsentsOfAsync(clientId)).Single().Status.Should().Be(CardConsentStatus.Pending);
    }

    /// <summary>A caller without a session is refused.</summary>
    [Fact]
    public async Task Complete_Anonymous_RefusesUnauthorizedAsync()
    {
        var response = await CompleteAsync(factory.CreateClient(), "sess_1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
