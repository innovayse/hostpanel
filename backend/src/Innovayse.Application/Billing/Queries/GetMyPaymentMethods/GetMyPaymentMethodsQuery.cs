namespace Innovayse.Application.Billing.Queries.GetMyPaymentMethods;

/// <summary>
/// Query to list the authenticated client's saved payment methods.
/// The controller extracts <paramref name="UserId"/> from the JWT sub claim.
/// </summary>
/// <param name="UserId">The authenticated user's Identity ID.</param>
public record GetMyPaymentMethodsQuery(string UserId);
