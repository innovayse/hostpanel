namespace Innovayse.Application.Billing.Commands.RemovePaymentMethod;

/// <summary>
/// Command to remove one of the authenticated client's saved payment methods.
/// The controller extracts <paramref name="UserId"/> from the JWT sub claim.
/// </summary>
/// <param name="UserId">The authenticated user's Identity ID.</param>
/// <param name="PaymentMethodId">The Stripe PaymentMethod ID to remove.</param>
public record RemovePaymentMethodCommand(string UserId, string PaymentMethodId);
