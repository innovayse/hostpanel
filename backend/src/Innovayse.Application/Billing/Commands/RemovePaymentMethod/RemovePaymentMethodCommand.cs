namespace Innovayse.Application.Billing.Commands.RemovePaymentMethod;

/// <summary>
/// Command to remove one of the authenticated client's saved payment methods.
/// </summary>
/// <remarks>
/// Carries no user id. Whose account is resolved inside the handler from the credential.
/// </remarks>
/// <param name="PaymentMethodId">The Stripe PaymentMethod ID to remove.</param>
public record RemovePaymentMethodCommand(string PaymentMethodId);
