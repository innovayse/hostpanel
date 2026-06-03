namespace Innovayse.API.Billing.Requests;

using Innovayse.Domain.Billing;

/// <summary>Request body for PUT /api/billing/quotes/{id} — updates an existing quote.</summary>
public sealed class UpdateQuoteRequest
{
    /// <summary>Gets or initialises the new quote subject / title.</summary>
    public required string Subject { get; init; }

    /// <summary>Gets or initialises the new lifecycle stage.</summary>
    public required QuoteStage Stage { get; init; }

    /// <summary>Gets or initialises the new expiry date (UTC).</summary>
    public required DateTimeOffset ValidUntil { get; init; }

    /// <summary>Gets or initialises optional notes or terms; null to clear.</summary>
    public string? Notes { get; init; }

    /// <summary>Gets or initialises the proposal text; null to clear.</summary>
    public string? ProposalText { get; init; }

    /// <summary>Gets or initialises the customer-facing footer notes; null to clear.</summary>
    public string? CustomerNotes { get; init; }

    /// <summary>Gets or initialises the admin-only internal notes; null to clear.</summary>
    public string? AdminNotes { get; init; }

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

    /// <summary>Gets or initialises the discount percentage (0–100).</summary>
    public decimal DiscountPercent { get; init; }

    /// <summary>Gets or initialises whether this item is taxed.</summary>
    public bool Taxed { get; init; }

    /// <summary>Gets or initialises whether this item should be deleted.</summary>
    public bool IsDeleted { get; init; }
}
