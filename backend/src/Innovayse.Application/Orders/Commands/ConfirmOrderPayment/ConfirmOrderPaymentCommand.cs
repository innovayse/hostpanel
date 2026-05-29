namespace Innovayse.Application.Orders.Commands.ConfirmOrderPayment;

/// <summary>
/// Command to confirm a Stripe payment for an order, mark the linked invoice as paid,
/// accept the order, and dispatch service creation for each order item.
/// </summary>
/// <param name="OrderId">The order primary key.</param>
/// <param name="PaymentIntentId">The Stripe PaymentIntent identifier to verify.</param>
public record ConfirmOrderPaymentCommand(int OrderId, string PaymentIntentId);
