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
/// that the card was saved. A card with no active consent is never charged automatically. A withdrawn
/// agreement is final: it is never activated again, so a client who turned automatic charges off cannot be
/// charged by a confirmation that lands afterwards. A concurrency token on the row makes the database refuse
/// whichever of an activation and a withdrawal was based on the older state.
/// </remarks>
public sealed class CardChargeConsent : BaseEntity
{
    /// <summary>The version of the consent wording the client was shown; bump it when the text changes.</summary>
    public const string CurrentTextVersion = "card-consent-2026-11-1";

    /// <summary>Gets the client who agreed.</summary>
    public int ClientId { get; private set; }

    /// <summary>Gets the payment gateway module the card is saved with (for example "stripe"); the agreement covers charges through that module only.</summary>
    public string GatewayModuleId { get; private set; } = string.Empty;

    /// <summary>Gets the gateway's id of the save session that stores the card; the row is tied to it until confirmed.</summary>
    public string GatewaySessionId { get; private set; } = string.Empty;

    /// <summary>Gets the saved method the agreement covers, as the gateway names it; set when the agreement becomes active.</summary>
    public string? GatewayMethodRef { get; private set; }

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
    public CardConsentRevocationReason? RevocationReason { get; private set; }

    /// <summary>EF Core parameterless constructor: do not call directly.</summary>
    private CardChargeConsent() { }

    /// <summary>Records that a client agreed to keep a card, before the gateway confirms it was saved.</summary>
    /// <param name="clientId">The client who agreed.</param>
    /// <param name="gatewayModuleId">The payment gateway module the card is saved with.</param>
    /// <param name="gatewaySessionId">The gateway's save session that stores the card.</param>
    /// <param name="ipAddress">The address the request came from, or <see langword="null"/> when unknown.</param>
    /// <param name="userAgent">The browser the request came from, or <see langword="null"/> when unknown.</param>
    /// <param name="now">The current moment.</param>
    /// <returns>A new, pending agreement.</returns>
    public static CardChargeConsent Create(
        int clientId, string gatewayModuleId, string gatewaySessionId, string? ipAddress, string? userAgent, DateTimeOffset now) =>
        new()
        {
            ClientId = clientId,
            GatewayModuleId = gatewayModuleId,
            GatewaySessionId = gatewaySessionId,
            TextVersion = CurrentTextVersion,
            Status = CardConsentStatus.Pending,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            RequestedAt = now,
        };

    /// <summary>Makes the agreement active once the gateway has confirmed the card was saved.</summary>
    /// <param name="methodRef">The saved method, as the gateway names it.</param>
    /// <param name="now">The current moment.</param>
    /// <returns><see langword="true"/> when the agreement was pending and is now active; <see langword="false"/> when it was already active or withdrawn, and nothing changed.</returns>
    public bool Activate(string methodRef, DateTimeOffset now)
    {
        if (Status != CardConsentStatus.Pending)
        {
            return false;
        }

        GatewayMethodRef = methodRef;
        Status = CardConsentStatus.Active;
        ActivatedAt = now;
        return true;
    }

    /// <summary>Withdraws the agreement; the card is never charged automatically after this.</summary>
    /// <param name="reason">Why it was withdrawn, for the record.</param>
    /// <param name="now">The current moment.</param>
    /// <returns><see langword="true"/> when the agreement was pending or active and is now withdrawn; <see langword="false"/> when it already was, and nothing changed.</returns>
    public bool Revoke(CardConsentRevocationReason reason, DateTimeOffset now)
    {
        if (Status == CardConsentStatus.Revoked)
        {
            return false;
        }

        Status = CardConsentStatus.Revoked;
        RevokedAt = now;
        RevocationReason = reason;
        return true;
    }
}
