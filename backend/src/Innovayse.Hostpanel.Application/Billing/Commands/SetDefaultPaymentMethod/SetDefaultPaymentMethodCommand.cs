namespace Innovayse.Hostpanel.Application.Billing.Commands.SetDefaultPaymentMethod;

/// <summary>
/// Command to make one of the authenticated client's saved payment methods the default.
/// </summary>
/// <remarks>
/// Carries no user id. Whose account is resolved inside the handler from the credential.
/// </remarks>
/// <param name="GatewayModuleId">
/// The payment gateway module that holds the method. Nullable for the same reason as
/// <paramref name="PaymentMethodId"/>: the controller overlays it from the route.
/// </param>
/// <param name="PaymentMethodId">
/// The gateway's reference for the method to make the default. Nullable because the controller overlays it
/// from the route (<c>command with { PaymentMethodId = id }</c>): a non-nullable member of a
/// bound body is implicitly required, so the request would be refused before the overlay could
/// run.
/// </param>
/// <param name="SetAsDefault">
/// Must be <see langword="true"/>. The card gateway allows no other update to an existing card, its
/// number, brand and expiry cannot be changed once issued, so this is the only update
/// <c>PUT api/me/payment-methods/{module}/{id}</c> accepts, and a body that omits or clears it is
/// refused by <c>SetDefaultPaymentMethodValidator</c> rather than silently accepted and
/// ignored.
/// </param>
public sealed record SetDefaultPaymentMethodCommand(string? GatewayModuleId, string? PaymentMethodId, bool SetAsDefault);
