namespace Innovayse.API.Orders.Requests;

/// <summary>Request body for starting a hosted-gateway payment.</summary>
/// <param name="Module">The payment plugin id (e.g. "innovayse-inecobank").</param>
/// <param name="ReturnUrl">Absolute URL the gateway redirects the payer back to.</param>
public sealed record StartGatewayPaymentRequest(string Module, string ReturnUrl);
