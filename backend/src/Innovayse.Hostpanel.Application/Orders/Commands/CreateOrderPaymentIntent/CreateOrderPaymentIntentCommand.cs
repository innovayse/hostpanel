using Innovayse.Hostpanel.Application.Billing.Common;

namespace Innovayse.Hostpanel.Application.Orders.Commands.CreateOrderPaymentIntent;

/// <summary>
/// Opens a card-payment intent at the card gateway for the invoice an order was billed on, so the payer's
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
/// <see cref="Innovayse.Hostpanel.Domain.Orders.Order.PaymentToken"/> for why an order id alone is not
/// enough.
/// </param>
/// <param name="SaveCard">
/// Whether the payer ticked the box offering to keep the card for automatic renewal charges. Off unless the
/// payer says so; when on, the card is saved on the client's gateway customer and the agreement is recorded.
/// </param>
/// <param name="Origin">
/// Where the request came from, kept with the agreement as proof. Set by the endpoint from the request itself,
/// never taken from the body.
/// </param>
public sealed record CreateOrderPaymentIntentCommand(int OrderId, string? PaymentToken, bool SaveCard = false, CardConsentOrigin? Origin = null);
