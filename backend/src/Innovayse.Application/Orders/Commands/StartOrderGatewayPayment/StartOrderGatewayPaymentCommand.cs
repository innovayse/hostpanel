namespace Innovayse.Application.Orders.Commands.StartOrderGatewayPayment;

/// <summary>Starts a hosted-gateway payment for the invoice an order was billed on.</summary>
/// <remarks>
/// The order-shaped way in to the same work <c>StartGatewayPaymentCommand</c> does. Checkout knows
/// the order it just placed, not the invoice generated behind it, and resolving one from the other
/// is a lookup that belongs on this side of the HTTP edge rather than in the endpoint.
/// </remarks>
/// <param name="OrderId">The order to pay.</param>
/// <param name="Module">The payment plugin id (e.g. "innovayse-inecobank").</param>
/// <param name="ReturnUrl">Absolute URL the gateway redirects the payer back to.</param>
/// <param name="PaymentToken">
/// The order's payment token, proving the caller is the payer this order was handed to.
/// Checkout is open to guests, so there is no credential to authorise against; see
/// <see cref="Innovayse.Domain.Orders.Order.PaymentToken"/> for why an order id alone is not
/// enough.
/// </param>
public sealed record StartOrderGatewayPaymentCommand(
    int OrderId, string Module, string ReturnUrl, string? PaymentToken);
