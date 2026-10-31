using Innovayse.Hostpanel.Application.Billing.Common;

namespace Innovayse.Hostpanel.Application.Billing.Commands.CreateMySetupIntent;

/// <summary>
/// Opens a setup intent so the caller can save a card without paying anything, and records the caller's
/// agreement that the card may be charged for renewals without them.
/// </summary>
/// <remarks>Carries no user id: whose account is resolved inside the handler from the credential.</remarks>
/// <param name="ConsentAccepted">Whether the caller ticked the agreement. Saving a card is refused without it.</param>
/// <param name="Origin">Where the request came from, kept with the agreement as proof. Set by the endpoint from the request itself, never taken from the body.</param>
public sealed record CreateMySetupIntentCommand(bool ConsentAccepted, CardConsentOrigin? Origin = null);
