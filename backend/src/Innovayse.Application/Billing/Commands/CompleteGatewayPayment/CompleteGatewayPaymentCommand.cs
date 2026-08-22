namespace Innovayse.Application.Billing.Commands.CompleteGatewayPayment;

/// <summary>
/// Verifies a hosted-gateway payment against the gateway and, when paid, marks the
/// invoice paid and fulfills the linked order. Idempotent: an already-paid invoice
/// returns "paid" without touching the gateway.
/// </summary>
/// <param name="InvoiceId">The invoice whose gateway session should be verified.</param>
public sealed record CompleteGatewayPaymentCommand(int InvoiceId);
