using System.Net;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>DELETE /api/me/payment-methods/auto-charge</c> against Postgres: the client turns automatic card charges
/// off, every one of their open agreements is withdrawn with the history kept, nobody else's is touched, and a
/// confirmation that wins the race for a row never keeps the agreement alive.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class RevokeMyCardConsentsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The route under test.</summary>
    private const string Route = "/api/me/payment-methods/auto-charge";

    /// <summary>Active and pending agreements are both withdrawn, and a revoked row keeps its reason and moment.</summary>
    [Fact]
    public async Task Revoke_OpenAgreements_WithdrawsAllAsync()
    {
        var gateway = StoredCredentialHost.Create(factory);
        var (caller, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, gateway.Host, "revoke");
        await gateway.AddConsentAsync(clientId, "pm_revoke_1");
        await gateway.AddConsentAsync(clientId, methodRef: null);

        var response = await caller.DeleteAsync(Route);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var rows = await gateway.ConsentsOfAsync(clientId);
        rows.Should().HaveCount(2).And.OnlyContain(c =>
            c.Status == CardConsentStatus.Revoked && c.RevokedAt != null && c.RevocationReason == CardConsentRevocationReason.WithdrawnByClient);
    }

    /// <summary>A client with no agreement gets the same quiet success.</summary>
    [Fact]
    public async Task Revoke_NothingToWithdraw_SucceedsAsync()
    {
        var gateway = StoredCredentialHost.Create(factory);
        var (caller, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, gateway.Host, "revoke-none");

        var response = await caller.DeleteAsync(Route);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await gateway.ConsentsOfAsync(clientId)).Should().BeEmpty();
    }

    /// <summary>Another client's agreement stays active.</summary>
    [Fact]
    public async Task Revoke_OtherClientsAgreement_IsUntouchedAsync()
    {
        var gateway = StoredCredentialHost.Create(factory);
        var (caller, _) = await BillingTestSeeds.RegisterClientAsync(factory, gateway.Host, "revoke");
        var (_, otherId) = await BillingTestSeeds.RegisterClientAsync(factory, gateway.Host, "revoke-other");
        await gateway.AddConsentAsync(otherId, "pm_revoke_1");

        await caller.DeleteAsync(Route);

        (await gateway.ConsentsOfAsync(otherId)).Should().ContainSingle().Which.Status.Should().Be(CardConsentStatus.Active);
    }

    /// <summary>A withdrawn agreement keeps its first reason and moment when the client turns the charges off again.</summary>
    [Fact]
    public async Task Revoke_AlreadyRevokedAgreement_IsLeftAsItWasAsync()
    {
        var gateway = StoredCredentialHost.Create(factory);
        var (caller, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, gateway.Host, "revoke-twice");
        await gateway.AddConsentAsync(clientId, "pm_revoke_1");
        await caller.DeleteAsync(Route);
        var first = (await gateway.ConsentsOfAsync(clientId)).Single();

        var again = await caller.DeleteAsync(Route);

        again.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var after = (await gateway.ConsentsOfAsync(clientId)).Single();
        after.RevokedAt.Should().Be(first.RevokedAt);
    }

    /// <summary>
    /// A confirmation activates a Pending agreement between the withdrawal's read and its save: the save fails on
    /// the stale row, the withdrawal reads again and ends up Revoked, never Active.
    /// </summary>
    [Fact]
    public async Task Revoke_ConfirmationWinsTheRace_IsRetriedAndRevokedWinsAsync()
    {
        var race = new ConsentRaceInterceptor((db, id) => db.Set<CardChargeConsent>()
            .Where(c => c.Id == id)
            .ExecuteUpdateAsync(set => set
                .SetProperty(c => c.Status, CardConsentStatus.Active)
                .SetProperty(c => c.GatewayMethodRef, "pm_raced")));
        var gateway = StoredCredentialHost.Create(factory, race);
        var (caller, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, gateway.Host, "revoke-race");
        await gateway.AddConsentAsync(clientId, methodRef: null);

        var response = await caller.DeleteAsync(Route);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        race.Fired.Should().BeTrue();
        var row = (await gateway.ConsentsOfAsync(clientId)).Single();
        row.Status.Should().Be(CardConsentStatus.Revoked);
        row.RevocationReason.Should().Be(CardConsentRevocationReason.WithdrawnByClient);
    }

    /// <summary>A caller without a session is refused.</summary>
    [Fact]
    public async Task Revoke_Anonymous_RefusesUnauthorizedAsync()
    {
        var response = await factory.CreateClient().DeleteAsync(Route);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
