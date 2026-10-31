namespace Innovayse.Hostpanel.Application.Billing.Common.Enums;

/// <summary>
/// Why a card gateway call did not complete. The adapter translates the gateway's own errors to
/// it, so no gateway text or exception ever reaches a handler or a response.
/// </summary>
public enum CardGatewayFailure
{
    /// <summary>The gateway answered and refused the request (a 4xx); repeating it unchanged will not help.</summary>
    Refused = 0,

    /// <summary>The gateway could not be reached or failed on its side (a 5xx, a timeout, a network error).</summary>
    Unavailable = 1,

    /// <summary>The deployment carries no card gateway secret key, so the gateway was never called.</summary>
    NotConfigured = 2,

    /// <summary>
    /// The gateway took no money because the cardholder must authenticate (3-D Secure) and nobody is
    /// there to do it. Unlike <see cref="Refused"/> the card is fine: paying the invoice with the
    /// cardholder present can succeed.
    /// </summary>
    AuthenticationRequired = 3,
}
