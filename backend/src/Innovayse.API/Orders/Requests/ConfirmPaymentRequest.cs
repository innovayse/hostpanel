namespace Innovayse.API.Orders.Requests;

/// <summary>Request body for confirming a Stripe payment.</summary>
/// <param name="PaymentIntentId">The Stripe PaymentIntent identifier.</param>
public record ConfirmPaymentRequest(string PaymentIntentId);
