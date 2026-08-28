namespace Innovayse.Application.Billing.Queries.GetMyPaymentMethods;

/// <summary>
/// Query to list the authenticated client's saved payment methods.
/// </summary>
/// <remarks>
/// Carries no user id. Whose payment methods is resolved inside the handler from the
/// credential, so there is no field a caller could set to read somebody else's cards.
/// </remarks>
public record GetMyPaymentMethodsQuery();
