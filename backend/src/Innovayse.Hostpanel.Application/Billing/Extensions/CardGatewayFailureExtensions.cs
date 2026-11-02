using Innovayse.Hostpanel.Application.Billing.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Application.Billing.Extensions;

/// <summary>Turns a card gateway failure into the error code a payment-method use case answers with.</summary>
public static class CardGatewayFailureExtensions
{
    /// <summary>The error code for a failed saved-payment-method call (list, default change, detach, charge lookup).</summary>
    /// <param name="failure">Why the gateway call failed.</param>
    /// <returns>
    /// <c>PAYMENT_GATEWAY_SERVICE_UNAVAILABLE</c> (503) when the gateway could not be reached, so the
    /// call may be retried; <c>PAYMENT_METHOD_UNAVAILABLE</c> (409) when the deployment has no card
    /// gateway key; otherwise <c>PAYMENT_METHOD_UPSTREAM_FAILED</c> (502).
    /// </returns>
    public static string ToPaymentMethodErrorCode(this CardGatewayFailure failure) => failure switch
    {
        CardGatewayFailure.Unavailable => HostpanelErrorCodes.PAYMENT_GATEWAY_SERVICE_UNAVAILABLE,
        CardGatewayFailure.NotConfigured => HostpanelErrorCodes.PAYMENT_METHOD_UNAVAILABLE,
        _ => HostpanelErrorCodes.PAYMENT_METHOD_UPSTREAM_FAILED,
    };
}
