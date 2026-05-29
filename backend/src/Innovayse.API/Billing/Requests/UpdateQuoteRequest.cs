namespace Innovayse.API.Billing.Requests;

using Innovayse.Domain.Billing;

/// <summary>Request body for PUT /api/billing/quotes/{id} — updates an existing quote.</summary>
public sealed class UpdateQuoteRequest
{
    /// <summary>Gets or initialises the new quote subject / title.</summary>
    public required string Subject { get; init; }

    /// <summary>Gets or initialises the new lifecycle status.</summary>
    public required QuoteStatus Status { get; init; }

    /// <summary>Gets or initialises the new expiry date (UTC).</summary>
    public required DateTimeOffset ExpiryDate { get; init; }

    /// <summary>Gets or initialises optional notes or terms; null to clear.</summary>
    public string? Notes { get; init; }

    /// <summary>Gets or initialises the full list of item changes.</summary>
    public required IReadOnlyList<UpdateQuoteItemRequest> Items { get; init; }
}

/// <summary>A single line item entry in an update-quote request.</summary>
public sealed class UpdateQuoteItemRequest
{
    /// <summary>Gets or initialises the existing item ID; null for new items.</summary>
    public int? Id { get; init; }

    /// <summary>Gets or initialises the human-readable description.</summary>
    public required string Description { get; init; }

    /// <summary>Gets or initialises the price per unit.</summary>
    public required decimal UnitPrice { get; init; }

    /// <summary>Gets or initialises the number of units.</summary>
    public required int Quantity { get; init; }

    /// <summary>Gets or initialises whether this item should be deleted.</summary>
    public bool IsDeleted { get; init; }
}
