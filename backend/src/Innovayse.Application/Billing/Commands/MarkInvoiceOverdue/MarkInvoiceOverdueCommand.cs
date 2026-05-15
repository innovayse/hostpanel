namespace Innovayse.Application.Billing.Commands.MarkInvoiceOverdue;

/// <summary>Command to transition an Unpaid invoice to Overdue status.</summary>
/// <param name="InvoiceId">The invoice to mark overdue.</param>
public record MarkInvoiceOverdueCommand(int InvoiceId);
