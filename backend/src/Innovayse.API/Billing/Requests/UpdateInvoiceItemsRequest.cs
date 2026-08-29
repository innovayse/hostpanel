namespace Innovayse.API.Billing.Requests;

/// <summary>Request body for PUT /api/billing/{id}/items — updates invoice line items.</summary>
public sealed class UpdateInvoiceItemsRequest
{
    /// <summary>Gets or initialises the list of item changes.</summary>
    public required IReadOnlyList<UpdateItemEntryRequest> Items { get; init; }
}
