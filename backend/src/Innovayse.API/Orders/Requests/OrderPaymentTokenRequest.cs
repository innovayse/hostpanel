namespace Innovayse.API.Orders.Requests;

/// <summary>
/// Request body for the order payment endpoints whose only input is the order's payment token.
/// </summary>
/// <remarks>
/// These two endpoints took no body at all before the token existed, being identified entirely by
/// the order id in the route. That is precisely what made them reachable by anyone who could count,
/// so a body carrying the token is the change, not an accident of shape.
/// </remarks>
/// <param name="PaymentToken">The order's payment token, returned when the order was placed.</param>
public sealed record OrderPaymentTokenRequest(string? PaymentToken);
