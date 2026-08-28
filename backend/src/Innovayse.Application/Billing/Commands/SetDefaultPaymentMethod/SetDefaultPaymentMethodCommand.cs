namespace Innovayse.Application.Billing.Commands.SetDefaultPaymentMethod;

/// <summary>
/// Command to make one of the authenticated client's saved payment methods the default.
/// </summary>
/// <remarks>
/// Carries no user id. Whose account is resolved inside the handler from the credential.
/// </remarks>
/// <param name="PaymentMethodId">The Stripe PaymentMethod ID to make the default.</param>
public record SetDefaultPaymentMethodCommand(string PaymentMethodId);
