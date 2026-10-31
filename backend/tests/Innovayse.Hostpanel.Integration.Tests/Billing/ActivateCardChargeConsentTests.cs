using Innovayse.Hostpanel.Application.Billing.Commands.ActivateCardChargeConsent;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <see cref="ActivateCardChargeConsentCommand"/> through the real <see cref="IMessageBus"/>: once the gateway
/// has confirmed the card a payment saved, the pending agreement of that intent becomes active for exactly that
/// card, and an intent with no agreement (the client did not tick the box) changes nothing.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ActivateCardChargeConsentTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A pending agreement becomes active for the card the gateway reported.</summary>
    [Fact]
    public async Task HandleAsync_PendingAgreement_BecomesActiveForThatCardAsync()
    {
        var (clientId, intent) = await SeedPendingAsync();

        var result = await InvokeAsync(new ActivateCardChargeConsentCommand(clientId, intent, "pm_checkout_1"));

        result.IsSuccess.Should().BeTrue();
        var row = await RowAsync(intent);
        row.Status.Should().Be(CardConsentStatus.Active);
        row.GatewayPaymentMethodId.Should().Be("pm_checkout_1");
        row.ActivatedAt.Should().NotBeNull();
    }

    /// <summary>An intent with no agreement (the box was not ticked) activates nothing.</summary>
    [Fact]
    public async Task HandleAsync_NoAgreementForTheIntent_ChangesNothingAsync()
    {
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);

        var result = await InvokeAsync(new ActivateCardChargeConsentCommand(clientId, $"pi_{Guid.NewGuid():N}", "pm_checkout_2"));

        result.IsSuccess.Should().BeTrue();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.CardChargeConsents.AnyAsync(c => c.ClientId == clientId)).Should().BeFalse();
    }

    /// <summary>Another client cannot activate an agreement that is not theirs.</summary>
    [Fact]
    public async Task HandleAsync_AgreementOfAnotherClient_StaysPendingAsync()
    {
        var (_, intent) = await SeedPendingAsync();
        var stranger = await BillingTestSeeds.SeedClientAsync(factory);

        await InvokeAsync(new ActivateCardChargeConsentCommand(stranger, intent, "pm_checkout_3"));

        (await RowAsync(intent)).Status.Should().Be(CardConsentStatus.Pending);
    }

    /// <summary>A withdrawn agreement does not come back.</summary>
    [Fact]
    public async Task HandleAsync_RevokedAgreement_StaysRevokedAsync()
    {
        var (clientId, intent) = await SeedPendingAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            (await context.CardChargeConsents.SingleAsync(c => c.GatewayIntentId == intent)).Revoke("test", DateTimeOffset.UtcNow);
            await context.SaveChangesAsync(CancellationToken.None);
        }

        await InvokeAsync(new ActivateCardChargeConsentCommand(clientId, intent, "pm_checkout_4"));

        (await RowAsync(intent)).Status.Should().Be(CardConsentStatus.Revoked);
    }

    /// <summary>Seeds a client with one pending agreement.</summary>
    /// <returns>The client and the gateway intent id of the agreement.</returns>
    private async Task<(int ClientId, string Intent)> SeedPendingAsync()
    {
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var intent = $"pi_{Guid.NewGuid():N}";
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        context.CardChargeConsents.Add(CardChargeConsent.Request(clientId, intent, CardConsentSource.PaymentCheckbox, null, null, DateTimeOffset.UtcNow));
        await context.SaveChangesAsync(CancellationToken.None);
        return (clientId, intent);
    }

    /// <summary>Reads the agreement of an intent.</summary>
    /// <param name="intent">The gateway intent id.</param>
    /// <returns>The agreement.</returns>
    private async Task<CardChargeConsent> RowAsync(string intent)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.CardChargeConsents.AsNoTracking().SingleAsync(c => c.GatewayIntentId == intent);
    }

    /// <summary>Dispatches the command on a fresh scope's bus.</summary>
    /// <param name="cmd">The command to send.</param>
    /// <returns>The handler's result.</returns>
    private async Task<Result> InvokeAsync(ActivateCardChargeConsentCommand cmd)
    {
        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        return await bus.InvokeAsync<Result>(cmd);
    }
}
