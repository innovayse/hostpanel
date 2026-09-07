namespace Innovayse.API.Orders.Requests;

/// <summary>Request body for confirming a Stripe payment.</summary>
/// <param name="PaymentIntentId">The Stripe PaymentIntent identifier.</param>
/// <param name="PaymentToken">The order's payment token, returned when the order was placed.</param>
public record ConfirmPaymentRequest(string PaymentIntentId, string? PaymentToken);
