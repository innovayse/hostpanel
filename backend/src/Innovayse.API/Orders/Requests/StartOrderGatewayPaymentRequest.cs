namespace Innovayse.API.Orders.Requests;

/// <summary>Request body for starting a hosted-gateway payment against an order.</summary>
/// <remarks>
/// Deliberately separate from <see cref="StartGatewayPaymentRequest"/>, which the client-area
/// endpoint uses. That one is reached with a credential and the caller's own invoices are the
/// only ones it can name; this one is open to guests, so it has to carry the order's payment
/// token as well. Sharing a single record would have put a token field on an endpoint that must
/// never read one.
/// </remarks>
/// <param name="Module">The payment plugin id (e.g. "innovayse-inecobank").</param>
/// <param name="ReturnUrl">Absolute URL the gateway redirects the payer back to.</param>
/// <param name="PaymentToken">The order's payment token, returned when the order was placed.</param>
public sealed record StartOrderGatewayPaymentRequest(string Module, string ReturnUrl, string? PaymentToken);
