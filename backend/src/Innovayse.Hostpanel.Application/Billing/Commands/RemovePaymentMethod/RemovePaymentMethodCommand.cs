namespace Innovayse.Hostpanel.Application.Billing.Commands.RemovePaymentMethod;

/// <summary>
/// Command to remove one of the authenticated client's saved payment methods.
/// </summary>
/// <remarks>
/// Carries no user id. Whose account is resolved inside the handler from the credential.
/// </remarks>
/// <param name="GatewayModuleId">The payment gateway module that holds the method.</param>
/// <param name="PaymentMethodId">The gateway's reference for the method to remove.</param>
public sealed record RemovePaymentMethodCommand(string GatewayModuleId, string PaymentMethodId);
