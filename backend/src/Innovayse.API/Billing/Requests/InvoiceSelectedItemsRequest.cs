namespace Innovayse.API.Billing.Requests;

/// <summary>Request body for POST /api/clients/{clientId}/billable-items/invoice-selected.</summary>
public sealed class InvoiceSelectedItemsRequest
{
    /// <summary>Gets or initialises the IDs of billable items to invoice.</summary>
    public required IReadOnlyList<int> BillableItemIds { get; init; }
}
