namespace Innovayse.Hostpanel.Application.Billing.Commands.ApplyMyInvoiceCredit;

/// <summary>Command for a client to pay part or all of one of their own invoices from their credit balance.</summary>
/// <remarks>
/// Carries no client id. Which account the invoice must belong to is resolved inside the handler from the credential.
/// </remarks>
/// <param name="InvoiceId">The invoice to apply credit to; the route's id wins over any value in the body.</param>
/// <param name="Amount">The credit to apply, in the invoice currency.</param>
public sealed record ApplyMyInvoiceCreditCommand(int InvoiceId, decimal Amount);
