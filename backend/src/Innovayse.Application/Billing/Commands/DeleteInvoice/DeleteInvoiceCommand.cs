namespace Innovayse.Application.Billing.Commands.DeleteInvoice;

/// <summary>Command to permanently delete a draft or cancelled invoice.</summary>
/// <param name="InvoiceId">The invoice to delete.</param>
public record DeleteInvoiceCommand(int InvoiceId);
