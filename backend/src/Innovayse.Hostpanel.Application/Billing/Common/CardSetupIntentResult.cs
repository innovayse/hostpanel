using Innovayse.Hostpanel.Application.Billing.Common.Enums;

namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>A setup intent the card gateway has opened, or why it could not open one.</summary>
/// <param name="SetupIntentId">The gateway's id of the setup intent, set when <paramref name="Failure"/> is <see langword="null"/>.</param>
/// <param name="ClientSecret">The secret the browser uses to confirm the card, set when <paramref name="Failure"/> is <see langword="null"/>.</param>
/// <param name="Failure">Why the gateway opened no setup intent, or <see langword="null"/> when it opened one.</param>
public sealed record CardSetupIntentResult(string? SetupIntentId, string? ClientSecret, CardGatewayFailure? Failure = null)
{
    /// <summary>Builds the answer for a call the gateway could not serve.</summary>
    /// <param name="failure">Why the gateway opened no setup intent.</param>
    /// <returns>A result with no intent carrying <paramref name="failure"/>.</returns>
    public static CardSetupIntentResult Failed(CardGatewayFailure failure) => new(null, null, failure);
}
