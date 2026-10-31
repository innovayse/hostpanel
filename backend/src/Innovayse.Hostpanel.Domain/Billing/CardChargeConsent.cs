using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Domain.Billing;

/// <summary>
/// A client's agreement that one saved card may be charged without them, kept as a row that is never
/// rewritten into something else: a grant, and later possibly its withdrawal. Stored in the
/// <c>CardChargeConsents</c> table.
/// </summary>
/// <remarks>
/// The row is created <see cref="CardConsentStatus.Pending"/> when the client agrees and becomes
/// <see cref="CardConsentStatus.Active"/> only once the card gateway confirms, from its own record,
/// that the card was saved. A card with no active consent is never charged automatically.
/// </remarks>
public sealed class CardChargeConsent : BaseEntity
{
    /// <summary>The version of the consent wording the client was shown; bump it when the text changes.</summary>
    public const string CurrentTextVersion = "card-consent-2026-11-1";

    /// <summary>Gets the client who agreed.</summary>
    public int ClientId { get; private set; }

    /// <summary>Gets the card gateway's id of the intent (payment or setup) that saves the card; the row is tied to it until confirmed.</summary>
    public string GatewayIntentId { get; private set; } = string.Empty;

    /// <summary>Gets the saved card the agreement covers, set when the agreement becomes active.</summary>
    public string? GatewayPaymentMethodId { get; private set; }

    /// <summary>Gets how the client agreed.</summary>
    public CardConsentSource Source { get; private set; }

    /// <summary>Gets the version of the consent wording the client was shown.</summary>
    public string TextVersion { get; private set; } = string.Empty;

    /// <summary>Gets where the agreement stands.</summary>
    public CardConsentStatus Status { get; private set; }

    /// <summary>Gets the address the client agreed from, as the request reported it.</summary>
    public string? IpAddress { get; private set; }

    /// <summary>Gets the browser the client agreed from, as the request reported it.</summary>
    public string? UserAgent { get; private set; }

    /// <summary>Gets when the client asked to save the card.</summary>
    public DateTimeOffset RequestedAt { get; private set; }

    /// <summary>Gets when the gateway confirmed the card was saved and the agreement became active.</summary>
    public DateTimeOffset? ActivatedAt { get; private set; }

    /// <summary>Gets when the agreement was withdrawn.</summary>
    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>Gets why the agreement was withdrawn.</summary>
    public string? RevokedReason { get; private set; }

    /// <summary>EF Core parameterless constructor: do not call directly.</summary>
    private CardChargeConsent() { }

    /// <summary>Records that a client agreed to keep a card, before the gateway confirms it was saved.</summary>
    /// <param name="clientId">The client who agreed.</param>
    /// <param name="gatewayIntentId">The gateway intent (payment or setup) that saves the card.</param>
    /// <param name="source">How the client agreed.</param>
    /// <param name="ipAddress">The address the request came from, or <see langword="null"/> when unknown.</param>
    /// <param name="userAgent">The browser the request came from, or <see langword="null"/> when unknown.</param>
    /// <param name="now">The current moment.</param>
    /// <returns>A new, pending agreement.</returns>
    public static CardChargeConsent Request(
        int clientId, string gatewayIntentId, CardConsentSource source, string? ipAddress, string? userAgent, DateTimeOffset now) =>
        new()
        {
            ClientId = clientId,
            GatewayIntentId = gatewayIntentId,
            Source = source,
            TextVersion = CurrentTextVersion,
            Status = CardConsentStatus.Pending,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            RequestedAt = now,
        };

    /// <summary>Makes the agreement active once the gateway has confirmed the card was saved.</summary>
    /// <param name="paymentMethodId">The saved card, as the gateway reports it.</param>
    /// <param name="now">The current moment.</param>
    /// <returns><see langword="true"/> when the agreement was pending and is now active; <see langword="false"/> when it was already active or withdrawn, and nothing changed.</returns>
    public bool Activate(string paymentMethodId, DateTimeOffset now)
    {
        if (Status != CardConsentStatus.Pending)
        {
            return false;
        }

        GatewayPaymentMethodId = paymentMethodId;
        Status = CardConsentStatus.Active;
        ActivatedAt = now;
        return true;
    }

    /// <summary>Withdraws the agreement; the card is never charged automatically after this.</summary>
    /// <param name="reason">Why it was withdrawn, for the record.</param>
    /// <param name="now">The current moment.</param>
    /// <returns><see langword="true"/> when the agreement was pending or active and is now withdrawn; <see langword="false"/> when it already was, and nothing changed.</returns>
    public bool Revoke(string reason, DateTimeOffset now)
    {
        if (Status == CardConsentStatus.Revoked)
        {
            return false;
        }

        Status = CardConsentStatus.Revoked;
        RevokedAt = now;
        RevokedReason = reason;
        return true;
    }
}
