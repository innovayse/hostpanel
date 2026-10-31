using Innovayse.Hostpanel.Application.Billing.Common.Enums;

namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>A payment intent the card gateway has opened, or why it could not open one.</summary>
/// <param name="ClientSecret">The secret the frontend uses to confirm the payment; set when <paramref name="Failure"/> is <see langword="null"/>.</param>
/// <param name="Failure">Why the gateway opened no intent, or <see langword="null"/> when it opened one.</param>
/// <param name="IntentId">The gateway's id of the intent, set when <paramref name="Failure"/> is <see langword="null"/>; the consent to keep the card is tied to it.</param>
public sealed record CardPaymentIntentResult(string? ClientSecret, CardGatewayFailure? Failure = null, string? IntentId = null);
