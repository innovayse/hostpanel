using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Billing.Extensions;

/// <summary>The few reads and writes of card charge agreements that more than one use case needs.</summary>
public static class CardConsentExtensions
{
    /// <summary>Finds the active agreement that lets one saved card be charged without the client.</summary>
    /// <param name="context">The data context.</param>
    /// <param name="clientId">The client.</param>
    /// <param name="paymentMethodId">The saved card.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The active agreement, or <see langword="null"/> when the card has none and must never be charged automatically.</returns>
    public static Task<CardChargeConsent?> FindActiveCardConsentAsync(
        this IHostpanelDbContext context, int clientId, string paymentMethodId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.CardChargeConsents.FirstOrDefaultAsync(
            c => c.ClientId == clientId && c.GatewayPaymentMethodId == paymentMethodId && c.Status == CardConsentStatus.Active, ct);
    }

    /// <summary>
    /// Makes the pending agreement tied to a gateway intent active, once the gateway has confirmed the card
    /// that intent saved. Stages the change; the caller saves.
    /// </summary>
    /// <param name="context">The data context.</param>
    /// <param name="clientId">The client who agreed; an agreement of another client is never touched.</param>
    /// <param name="gatewayIntentId">The payment or setup intent that saved the card.</param>
    /// <param name="paymentMethodId">The saved card, as the gateway reports it.</param>
    /// <param name="now">The current moment.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when an agreement is now active for that card (just now or already); <see langword="false"/> when there is none to activate.</returns>
    public static async Task<bool> ActivateCardConsentAsync(
        this IHostpanelDbContext context, int clientId, string gatewayIntentId, string paymentMethodId, DateTimeOffset now, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);

        var consent = await context.CardChargeConsents.FirstOrDefaultAsync(
            c => c.ClientId == clientId && c.GatewayIntentId == gatewayIntentId, ct);
        if (consent is null)
        {
            return false;
        }

        if (consent.Status == CardConsentStatus.Active && consent.GatewayPaymentMethodId == paymentMethodId)
        {
            return true;
        }

        return consent.Activate(paymentMethodId, now);
    }
}
