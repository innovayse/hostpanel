using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Application.Billing.Commands.RequestCardChargeConsent;

/// <summary>
/// Records that a client agreed to keep a card for automatic renewal charges, tied to the gateway intent
/// that saves it. The agreement stays pending until <c>ActivateCardChargeConsent</c> confirms the card was saved.
/// </summary>
/// <param name="ClientId">The client who agreed.</param>
/// <param name="GatewayIntentId">The payment or setup intent that saves the card.</param>
/// <param name="Source">How the client agreed.</param>
/// <param name="Origin">Where the request came from, kept as proof.</param>
public sealed record RequestCardChargeConsentCommand(int ClientId, string GatewayIntentId, CardConsentSource Source, CardConsentOrigin Origin);
