namespace Innovayse.Application.Billing.Commands.SetDefaultPaymentMethod;

/// <summary>
/// Command to make one of the authenticated client's saved payment methods the default.
/// The controller extracts <paramref name="UserId"/> from the JWT sub claim.
/// </summary>
/// <param name="UserId">The authenticated user's Identity ID.</param>
/// <param name="PaymentMethodId">The Stripe PaymentMethod ID to make the default.</param>
public record SetDefaultPaymentMethodCommand(string UserId, string PaymentMethodId);
