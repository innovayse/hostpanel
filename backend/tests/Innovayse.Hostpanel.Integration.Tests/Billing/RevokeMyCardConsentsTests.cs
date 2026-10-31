using System.Net;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>DELETE /api/me/payment-methods/auto-charge</c> against Postgres: the client turns automatic card charges
/// off, every one of their open agreements is withdrawn with the history kept, and nobody else's is touched.
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
        var (caller, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, null, "revoke");
        await AddAgreementAsync(clientId, active: true);
        await AddAgreementAsync(clientId, active: false);

        var response = await caller.DeleteAsync(Route);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var rows = await ConsentsOfAsync(clientId);
        rows.Should().HaveCount(2).And.OnlyContain(c => c.Status == CardConsentStatus.Revoked && c.RevokedAt != null && c.RevokedReason != null);
    }

    /// <summary>A client with no agreement gets the same quiet success.</summary>
    [Fact]
    public async Task Revoke_NothingToWithdraw_SucceedsAsync()
    {
        var (caller, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, null, "revoke");

        var response = await caller.DeleteAsync(Route);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ConsentsOfAsync(clientId)).Should().BeEmpty();
    }

    /// <summary>Another client's agreement stays active.</summary>
    [Fact]
    public async Task Revoke_OtherClientsAgreement_IsUntouchedAsync()
    {
        var (caller, _) = await BillingTestSeeds.RegisterClientAsync(factory, null, "revoke");
        var (_, otherId) = await BillingTestSeeds.RegisterClientAsync(factory, null, "revoke-other");
        await AddAgreementAsync(otherId, active: true);

        await caller.DeleteAsync(Route);

        (await ConsentsOfAsync(otherId)).Should().ContainSingle().Which.Status.Should().Be(CardConsentStatus.Active);
    }

    /// <summary>A caller without a session is refused.</summary>
    [Fact]
    public async Task Revoke_Anonymous_RefusesUnauthorizedAsync()
    {
        var response = await factory.CreateClient().DeleteAsync(Route);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Adds an agreement for a client straight to the database.</summary>
    /// <param name="clientId">The client.</param>
    /// <param name="active">Whether it is already active (otherwise pending).</param>
    private async Task AddAgreementAsync(int clientId, bool active)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var consent = CardChargeConsent.Request(clientId, $"seti_{Guid.NewGuid():N}", CardConsentSource.AddCard, null, null, DateTimeOffset.UtcNow);
        if (active)
        {
            consent.Activate("pm_revoke_1", DateTimeOffset.UtcNow);
        }

        context.CardChargeConsents.Add(consent);
        await context.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>Reads a client's card agreements.</summary>
    /// <param name="clientId">The client.</param>
    /// <returns>The agreements.</returns>
    private async Task<List<CardChargeConsent>> ConsentsOfAsync(int clientId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.CardChargeConsents.AsNoTracking().Where(c => c.ClientId == clientId).ToListAsync();
    }
}
