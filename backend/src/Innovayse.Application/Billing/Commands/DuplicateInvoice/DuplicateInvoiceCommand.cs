namespace Innovayse.Application.Billing.Commands.DuplicateInvoice;

/// <summary>Command to duplicate an invoice as a new draft.</summary>
/// <param name="InvoiceId">The invoice to duplicate.</param>
public record DuplicateInvoiceCommand(int InvoiceId);
