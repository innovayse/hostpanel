namespace Innovayse.Application.Billing.Commands.StartGatewayPayment;

/// <summary>
/// Starts a hosted-gateway payment for an invoice: registers the payment at the
/// gateway and records the session on the invoice.
/// </summary>
/// <param name="InvoiceId">The invoice to pay.</param>
/// <param name="Module">The payment plugin id (e.g. "innovayse-inecobank").</param>
/// <param name="ReturnUrl">Absolute URL the gateway redirects the payer back to.</param>
public sealed record StartGatewayPaymentCommand(int InvoiceId, string Module, string ReturnUrl);
