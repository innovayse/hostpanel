using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Common.Extensions;

/// <summary>The one check every use case runs before it asks the card gateway for anything.</summary>
public static class CardPaymentGatewayExtensions
{
    /// <summary>
    /// Refuses a use case that needs the card gateway when the deployment carries no gateway key, so
    /// the gateway is never called and no caller can answer a missing key with a 500.
    /// </summary>
    /// <param name="gateway">The card gateway the use case is about to call.</param>
    /// <returns>
    /// Success when the gateway is configured; otherwise <c>PAYMENT_METHOD_UNAVAILABLE</c> (409).
    /// </returns>
    public static Result RequireConfigured(this ICardPaymentGateway gateway)
    {
        ArgumentNullException.ThrowIfNull(gateway);

        return gateway.IsConfigured
            ? Result.Success()
            : Result.FailureCode(HostpanelErrorCodes.PAYMENT_METHOD_UNAVAILABLE);
    }
}
