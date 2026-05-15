namespace Innovayse.Application.Billing.Commands.CancelInvoice;

/// <summary>Command to cancel an unpaid invoice.</summary>
/// <param name="InvoiceId">The invoice to cancel.</param>
public record CancelInvoiceCommand(int InvoiceId);
