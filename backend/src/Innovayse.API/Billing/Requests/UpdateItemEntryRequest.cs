namespace Innovayse.API.Billing.Requests;

/// <summary>A single item entry within <see cref="UpdateInvoiceItemsRequest"/>.</summary>
public sealed class UpdateItemEntryRequest
{
    /// <summary>Gets or initialises the existing item ID; null for new items.</summary>
    public int? Id { get; init; }

    /// <summary>Gets or initialises the human-readable charge description.</summary>
    public required string Description { get; init; }

    /// <summary>Gets or initialises the price per unit.</summary>
    public required decimal UnitPrice { get; init; }

    /// <summary>Gets or initialises the number of units.</summary>
    public required int Quantity { get; init; }

    /// <summary>Gets or initialises whether this item should be deleted.</summary>
    public bool IsDeleted { get; init; }
}
