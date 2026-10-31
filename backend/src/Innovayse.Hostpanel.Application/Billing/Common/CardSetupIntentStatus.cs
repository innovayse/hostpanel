using Innovayse.Hostpanel.Application.Billing.Common.Enums;

namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>What the card gateway reports about a setup intent: whether the card was saved, and for whom.</summary>
/// <remarks>The customer and the payment method come from the gateway's own record, never from the browser.</remarks>
/// <param name="Succeeded"><see langword="true"/> only when the gateway saved the card.</param>
/// <param name="CustomerId">The customer the card was saved against.</param>
/// <param name="PaymentMethodId">The saved payment method, set when <paramref name="Succeeded"/> is true.</param>
/// <param name="Failure">Why the gateway could not be asked, or <see langword="null"/> when it answered.</param>
public sealed record CardSetupIntentStatus(bool Succeeded, string? CustomerId, string? PaymentMethodId, CardGatewayFailure? Failure = null)
{
    /// <summary>Builds the answer for a call the gateway could not serve.</summary>
    /// <param name="failure">Why the gateway could not be asked.</param>
    /// <returns>An unsuccessful status carrying <paramref name="failure"/>.</returns>
    public static CardSetupIntentStatus Failed(CardGatewayFailure failure) => new(false, null, null, failure);
}
