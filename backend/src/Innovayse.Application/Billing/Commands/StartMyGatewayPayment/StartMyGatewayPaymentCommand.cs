namespace Innovayse.Application.Billing.Commands.StartMyGatewayPayment;

/// <summary>Command for a client to start a hosted-gateway payment on one of their own invoices.</summary>
/// <remarks>
/// Carries no client id. Which account the invoice must belong to is resolved inside the handler
/// from the credential. The order checkout flow, which starts a gateway payment for an invoice it
/// has just created, dispatches <c>StartGatewayPaymentCommand</c> directly.
/// </remarks>
/// <param name="InvoiceId">The invoice to pay.</param>
/// <param name="Module">The payment plugin id (e.g. "innovayse-inecobank").</param>
/// <param name="ReturnUrl">Absolute URL the gateway redirects the payer back to.</param>
public sealed record StartMyGatewayPaymentCommand(int InvoiceId, string Module, string ReturnUrl);
