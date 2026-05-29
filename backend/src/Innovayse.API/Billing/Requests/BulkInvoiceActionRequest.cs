namespace Innovayse.API.Billing.Requests;

/// <summary>Request body for POST /api/billing/bulk — performs a bulk action on multiple invoices.</summary>
public sealed class BulkInvoiceActionRequest
{
    /// <summary>Gets or initialises the invoice IDs to act on.</summary>
    public required IReadOnlyList<int> InvoiceIds { get; init; }

    /// <summary>Gets or initialises the action: MarkPaid, MarkUnpaid, MarkCancelled, Duplicate, Delete.</summary>
    public required string Action { get; init; }
}
