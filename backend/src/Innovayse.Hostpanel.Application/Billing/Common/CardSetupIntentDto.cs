namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>A setup intent the browser confirms to save a card.</summary>
/// <param name="SetupIntentId">The id the portal hands back once the card is confirmed.</param>
/// <param name="ClientSecret">The secret the card form confirms the setup intent with.</param>
public sealed record CardSetupIntentDto(string SetupIntentId, string ClientSecret);
