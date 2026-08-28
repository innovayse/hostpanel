namespace Innovayse.Application.Billing.Commands.CompleteMyGatewayPayment;

/// <summary>Command for a client to verify a hosted-gateway payment on one of their own invoices.</summary>
/// <remarks>
/// Carries no client id. Which account the invoice must belong to is resolved inside the handler
/// from the credential. The reconciliation cron, which verifies every pending session and has no
/// caller at all, dispatches <c>CompleteGatewayPaymentCommand</c> directly -- which is precisely
/// why the check could not be added to that shared handler.
/// </remarks>
/// <param name="InvoiceId">The invoice whose gateway session should be verified.</param>
public sealed record CompleteMyGatewayPaymentCommand(int InvoiceId);
