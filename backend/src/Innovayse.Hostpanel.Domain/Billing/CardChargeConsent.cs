using Innovayse.Hostpanel.SharedKernel.Domain;
using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Billing;

/// <summary>
/// A client's consent to have a stored payment method of one gateway module charged without them present. The host
/// charges a method only while it holds an active consent for it under that module. Stored in the
/// <c>CardChargeConsents</c> table.
/// </summary>
/// <remarks>
/// The consent is recorded when the save session is opened, before the payer reaches the gateway step, so it has no
/// method yet: <see cref="AttachMethod"/> binds the method once the session completes. A revoked consent is final;
/// consenting again writes a new row. The row also keeps the evidence of the consent: the version of the wording the
/// payer saw, and the address and browser the consent came from.
/// </remarks>
public sealed class CardChargeConsent : BaseEntity, IAggregateRoot
{
    /// <summary>The longest consent wording version the table holds, in characters.</summary>
    public const int MaxTextVersionLength = 50;

    /// <summary>The longest IP address the table holds, in characters (an IPv6 address with a scope is 45 or fewer).</summary>
    public const int MaxIpAddressLength = 45;

    /// <summary>The longest browser identification the table holds, in characters; longer text is cut.</summary>
    public const int MaxUserAgentLength = 512;

    /// <summary>Gets the client who consented.</summary>
    public int ClientId { get; private set; }

    /// <summary>Gets the id of the payment plugin whose gateway holds the method.</summary>
    public string GatewayModuleId { get; private set; } = string.Empty;

    /// <summary>Gets the save session the consent was recorded for; it finds the consent again when the session completes.</summary>
    public string SessionId { get; private set; } = string.Empty;

    /// <summary>Gets the stored method the consent covers, or <see langword="null"/> until the save session completes.</summary>
    public string? MethodRef { get; private set; }

    /// <summary>Gets the version of the consent wording the client saw, or <see langword="null"/> for a consent recorded before versions were kept.</summary>
    public string? ConsentTextVersion { get; private set; }

    /// <summary>Gets the address the consent was given from, or <see langword="null"/> when the request carried none.</summary>
    public string? IpAddress { get; private set; }

    /// <summary>Gets the browser identification the consent was given with, or <see langword="null"/> when the request carried none.</summary>
    public string? UserAgent { get; private set; }

    /// <summary>Gets whether this is the method the host charges first for the client at its gateway module. At most one active consent per client and module is the default.</summary>
    public bool IsDefault { get; private set; }

    /// <summary>Gets when the client consented (UTC).</summary>
    public DateTimeOffset GrantedAt { get; private set; }

    /// <summary>Gets when the consent was revoked (UTC), or <see langword="null"/> while it stands.</summary>
    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>Gets whether the consent was revoked. A revoked consent is never active again.</summary>
    public bool IsRevoked => RevokedAt is not null;

    /// <summary>Gets whether the host may charge the method: a method is attached and the consent was not revoked.</summary>
    public bool IsActive => MethodRef is not null && RevokedAt is null;

    /// <summary>EF Core parameterless constructor: do not call directly.</summary>
    private CardChargeConsent() { }

    /// <summary>Records a client's consent for a save session that is about to be opened at the gateway.</summary>
    /// <param name="clientId">The client who consents.</param>
    /// <param name="gatewayModuleId">The payment plugin id.</param>
    /// <param name="sessionId">The save session the consent belongs to.</param>
    /// <param name="now">The current moment (UTC).</param>
    /// <param name="consentTextVersion">The version of the consent wording the client saw, if known.</param>
    /// <param name="ipAddress">The address the consent came from, if the request carried one; cut to <see cref="MaxIpAddressLength"/>.</param>
    /// <param name="userAgent">The browser identification the consent came with, if the request carried one; cut to <see cref="MaxUserAgentLength"/>.</param>
    /// <returns>A new, unsaved <see cref="CardChargeConsent"/> with no method yet.</returns>
    /// <exception cref="ArgumentException">A text argument is empty or white space, or the wording version is too long.</exception>
    public static CardChargeConsent Grant(
        int clientId,
        string gatewayModuleId,
        string sessionId,
        DateTimeOffset now,
        string? consentTextVersion = null,
        string? ipAddress = null,
        string? userAgent = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gatewayModuleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        if (consentTextVersion is { Length: > MaxTextVersionLength })
        {
            throw new ArgumentException($"The consent text version is at most {MaxTextVersionLength} characters.", nameof(consentTextVersion));
        }

        return new CardChargeConsent
        {
            ClientId = clientId,
            GatewayModuleId = gatewayModuleId,
            SessionId = sessionId,
            GrantedAt = now,
            ConsentTextVersion = consentTextVersion,
            IpAddress = ipAddress is { Length: > MaxIpAddressLength } ? ipAddress[..MaxIpAddressLength] : ipAddress,
            UserAgent = userAgent is { Length: > MaxUserAgentLength } ? userAgent[..MaxUserAgentLength] : userAgent,
        };
    }

    /// <summary>
    /// Binds the stored method to the consent once the save session completed. Asking again with the same method is a
    /// no-op, because completing a session is idempotent.
    /// </summary>
    /// <param name="methodRef">The stored method the plugin reported.</param>
    /// <exception cref="ArgumentException"><paramref name="methodRef"/> is empty or white space.</exception>
    /// <exception cref="InvalidOperationException">The consent is revoked, or already covers a different method.</exception>
    public void AttachMethod(string methodRef)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(methodRef);

        if (IsRevoked)
        {
            throw new InvalidOperationException("A revoked consent cannot take a method.");
        }

        if (MethodRef is not null)
        {
            if (!string.Equals(MethodRef, methodRef, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The consent already covers a different method.");
            }

            return;
        }

        MethodRef = methodRef;
    }

    /// <summary>Whether <see cref="AttachMethod"/> would accept <paramref name="methodRef"/>: the consent has no method yet, or already covers this one.</summary>
    /// <param name="methodRef">The stored method the plugin reported.</param>
    /// <returns><see langword="false"/> when the consent already covers a different method.</returns>
    public bool CanAttach(string methodRef) =>
        MethodRef is null || string.Equals(MethodRef, methodRef, StringComparison.Ordinal);

    /// <summary>
    /// Makes this consent's method the one the host charges first, and clears the flag on the client's other consents
    /// at the same gateway module, so at most one is the default.
    /// </summary>
    /// <param name="siblings">The client's other consents under the same module; those of another client or module, and this one, are left alone.</param>
    /// <exception cref="InvalidOperationException">The consent is not active: it has no method yet, or was revoked.</exception>
    public void SetDefault(IEnumerable<CardChargeConsent> siblings)
    {
        ArgumentNullException.ThrowIfNull(siblings);
        if (!IsActive)
        {
            throw new InvalidOperationException("Only an active consent can be the default.");
        }

        foreach (var other in siblings)
        {
            if (!ReferenceEquals(other, this) && other.ClientId == ClientId && other.GatewayModuleId == GatewayModuleId)
            {
                other.IsDefault = false;
            }
        }

        IsDefault = true;
    }

    /// <summary>Revokes the consent. Final; revoking a revoked consent changes nothing. A revoked consent is no longer the default.</summary>
    /// <param name="now">The current moment (UTC).</param>
    public void Revoke(DateTimeOffset now)
    {
        RevokedAt ??= now;
        IsDefault = false;
    }
}
