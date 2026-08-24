namespace Innovayse.Providers.Inecobank;

/// <summary>
/// The Inecobank (Armenian Card) <c>getOrderStatusExtended.do</c> <c>orderStatus</c> protocol
/// codes this gateway maps, as described in the bank's merchant API manual. Only errorCode 0
/// responses carry a meaningful <c>orderStatus</c> — see <see cref="InecobankPaymentGateway.GetStatusAsync"/>.
/// Values not mapped by name here (0, 1, 5) are treated as pending by the gateway's switch.
/// </summary>
internal enum InecobankOrderStatusCode
{
    /// <summary>The manual's "order registered but not paid" state.</summary>
    Registered = 0,

    /// <summary>The manual's "pre-authorized amount held" state.</summary>
    PreAuthorized = 1,

    /// <summary>The manual's "deposited" state: funds were captured, the payment is complete.</summary>
    Deposited = 2,

    /// <summary>The manual's "authorization reversed" state: the hold was cancelled before deposit.</summary>
    AuthorizationReversed = 3,

    /// <summary>The manual's "refunded" state: a completed deposit was subsequently refunded.</summary>
    Refunded = 4,

    /// <summary>The manual's "ACS authentication initiated" (3-D Secure in progress) state.</summary>
    AcsAuthenticationInitiated = 5,

    /// <summary>The manual's "authorization declined" state: the issuer rejected the authorization.</summary>
    AuthorizationDeclined = 6,
}
