namespace Innovayse.Application.Orders.Commands.CompleteOrderGatewayPayment;

/// <summary>
/// Verifies the hosted-gateway payment on the invoice an order was billed on and, when the
/// gateway reports it paid, settles the invoice and fulfills the order.
/// </summary>
/// <remarks>
/// The order-shaped way in to <c>CompleteGatewayPaymentCommand</c>, for the checkout return leg
/// where the payer's browser comes back knowing only the order it started from.
/// </remarks>
/// <param name="OrderId">The order whose gateway session should be verified.</param>
public sealed record CompleteOrderGatewayPaymentCommand(int OrderId);
