using Innovayse.Hostpanel.Application.Billing.Common;

namespace Innovayse.Hostpanel.Application.Billing.Commands.BeginMyCardSave;

/// <summary>
/// Starts saving a payment method without paying anything, and records the caller's agreement that the method
/// may be charged for renewals without them.
/// </summary>
/// <remarks>Carries no user id: whose account is resolved inside the handler from the credential.</remarks>
/// <param name="GatewayModuleId">The payment gateway module to save the method with (for example "stripe").</param>
/// <param name="ReturnUrl">The portal page a gateway that sends the payer away brings them back to.</param>
/// <param name="ConsentAccepted">Whether the caller ticked the agreement. Saving a method is refused without it.</param>
/// <param name="Origin">Where the request came from, kept with the agreement as proof. Set by the endpoint from the request itself, never taken from the body.</param>
public sealed record BeginMyCardSaveCommand(
    string GatewayModuleId,
    string ReturnUrl,
    bool ConsentAccepted,
    CardConsentOrigin? Origin = null);
