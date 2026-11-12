namespace Innovayse.Hostpanel.Application.Billing.Common.Enums;

/// <summary>
/// How the payer's browser is taken through the hosted step of a gateway payment, as the portal must act on it. It mirrors
/// the plugin contract's session kinds one to one, so the wire does not depend on a type of the plugin SDK.
/// </summary>
public enum GatewaySessionKind
{
    /// <summary>The browser is sent to the address with a plain GET; the payment is read when the payer comes back.</summary>
    Redirect = 1,

    /// <summary>The browser posts the described form to the gateway's hosted page.</summary>
    FormPost = 2,

    /// <summary>
    /// The browser is sent to the address of a step the payer finishes there. The host then waits for the return request,
    /// hands its parameters to the plugin and only afterwards reads the payment status.
    /// </summary>
    BrowserStep = 3,
}
