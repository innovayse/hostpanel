namespace Innovayse.Application.Billing.Commands.BulkInvoiceAction;

/// <summary>Command to perform a bulk action on multiple invoices.</summary>
/// <param name="InvoiceIds">The invoice IDs to act on.</param>
/// <param name="Action">The action to perform: MarkPaid, MarkUnpaid, MarkCancelled, Duplicate, Delete.</param>
public record BulkInvoiceActionCommand(IReadOnlyList<int> InvoiceIds, string Action);
