namespace Innovayse.Application.Orders.Commands.CreateOrderPaymentIntent;

/// <summary>
/// Opens a card-payment intent at Stripe for the invoice an order was billed on, so the payer's
/// browser can confirm the charge itself without the card ever reaching this backend.
/// </summary>
/// <remarks>
/// Named by the order rather than the invoice because that is what the checkout flow has in hand;
/// which invoice belongs to it is settled in the handler, where the refusals for an order that
/// does not exist or was never invoiced live too.
/// </remarks>
/// <param name="OrderId">The order being paid.</param>
/// <param name="PaymentToken">
/// The order's payment token, proving the caller is the payer this order was handed to.
/// Checkout is open to guests, so there is no credential to authorise against; see
/// <see cref="Innovayse.Domain.Orders.Order.PaymentToken"/> for why an order id alone is not
/// enough.
/// </param>
public sealed record CreateOrderPaymentIntentCommand(int OrderId, string? PaymentToken);
