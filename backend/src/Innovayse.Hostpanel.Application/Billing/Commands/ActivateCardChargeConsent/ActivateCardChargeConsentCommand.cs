namespace Innovayse.Hostpanel.Application.Billing.Commands.ActivateCardChargeConsent;

/// <summary>
/// Makes the pending agreement tied to a gateway intent active, once the gateway has confirmed from its own
/// record that the intent saved a card. Does nothing when no agreement was asked for.
/// </summary>
/// <param name="ClientId">The client who agreed.</param>
/// <param name="GatewayIntentId">The payment intent that saved the card.</param>
/// <param name="PaymentMethodId">The saved card, as the gateway reports it.</param>
public sealed record ActivateCardChargeConsentCommand(int ClientId, string GatewayIntentId, string PaymentMethodId);
