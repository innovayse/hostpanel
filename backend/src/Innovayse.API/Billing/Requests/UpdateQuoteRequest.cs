namespace Innovayse.API.Billing.Requests;

using Innovayse.Domain.Billing;

<<<<<<< HEAD
/// <summary>Request body for PUT /api/billing/quotes/{id} — updates an existing quote.</summary>
=======
/// <summary>Request body for PUT /api/quotes/{id} — updates an existing quote.</summary>
>>>>>>> origin/main
public sealed class UpdateQuoteRequest
{
    /// <summary>Gets or initialises the new quote subject / title.</summary>
    public required string Subject { get; init; }

<<<<<<< HEAD
    /// <summary>Gets or initialises the new lifecycle status.</summary>
    public required QuoteStatus Status { get; init; }

    /// <summary>Gets or initialises the new expiry date (UTC).</summary>
    public required DateTimeOffset ExpiryDate { get; init; }

    /// <summary>Gets or initialises optional notes or terms; null to clear.</summary>
    public string? Notes { get; init; }
=======
    /// <summary>Gets or initialises the new lifecycle stage.</summary>
    public required QuoteStage Stage { get; init; }

    /// <summary>Gets or initialises the new expiry date; null for no expiry.</summary>
    public DateTimeOffset? ValidUntil { get; init; }

    /// <summary>Gets or initialises the new proposal text; null to clear.</summary>
    public string? ProposalText { get; init; }

    /// <summary>Gets or initialises the new customer-facing notes; null to clear.</summary>
    public string? CustomerNotes { get; init; }

    /// <summary>Gets or initialises the new private admin notes; null to clear.</summary>
    public string? AdminNotes { get; init; }
>>>>>>> origin/main

    /// <summary>Gets or initialises the full list of item changes.</summary>
    public required IReadOnlyList<UpdateQuoteItemRequest> Items { get; init; }
}

/// <summary>A single line item entry in an update-quote request.</summary>
public sealed class UpdateQuoteItemRequest
{
    /// <summary>Gets or initialises the existing item ID; null for new items.</summary>
    public int? Id { get; init; }

<<<<<<< HEAD
=======
    /// <summary>Gets or initialises the number of units.</summary>
    public required int Quantity { get; init; }

>>>>>>> origin/main
    /// <summary>Gets or initialises the human-readable description.</summary>
    public required string Description { get; init; }

    /// <summary>Gets or initialises the price per unit.</summary>
    public required decimal UnitPrice { get; init; }

<<<<<<< HEAD
    /// <summary>Gets or initialises the number of units.</summary>
    public required int Quantity { get; init; }
=======
    /// <summary>Gets or initialises the discount percentage (0–100).</summary>
    public decimal DiscountPercent { get; init; }

    /// <summary>Gets or initialises whether this item is subject to tax.</summary>
    public bool Taxed { get; init; }
>>>>>>> origin/main

    /// <summary>Gets or initialises whether this item should be deleted.</summary>
    public bool IsDeleted { get; init; }
}
