namespace Innovayse.API.Billing.Requests;

<<<<<<< HEAD
/// <summary>Request to create a quote.</summary>
public sealed class CreateQuoteRequest
{
    /// <summary>Gets or sets the client ID.</summary>
    public int ClientId { get; set; }

    /// <summary>Gets or sets the quote subject/title.</summary>
    public string Subject { get; set; } = null!;

    /// <summary>Gets or sets the quote expiry date.</summary>
    public DateTimeOffset ExpiryDate { get; set; }

    /// <summary>Gets or sets optional notes or terms.</summary>
    public string? Notes { get; set; }

    /// <summary>Gets or sets the line items for the quote.</summary>
    public List<CreateQuoteItemRequest> Items { get; set; } = [];
}

/// <summary>Request for a single quote line item.</summary>
public sealed class CreateQuoteItemRequest
{
    /// <summary>Gets or sets the human-readable description.</summary>
    public string Description { get; set; } = null!;

    /// <summary>Gets or sets the price per unit.</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>Gets or sets the quantity.</summary>
    public int Quantity { get; set; }
=======
using Innovayse.Domain.Billing;

/// <summary>Request body for POST /api/quotes — creates a new quote.</summary>
public sealed class CreateQuoteRequest
{
    /// <summary>Gets or initialises the client ID to quote.</summary>
    public required int ClientId { get; init; }

    /// <summary>Gets or initialises the quote subject / title.</summary>
    public required string Subject { get; init; }

    /// <summary>Gets or initialises the initial lifecycle stage.</summary>
    public required QuoteStage Stage { get; init; }

    /// <summary>Gets or initialises the expiry date; null for no expiry.</summary>
    public DateTimeOffset? ValidUntil { get; init; }

    /// <summary>Gets or initialises the proposal text; null to omit.</summary>
    public string? ProposalText { get; init; }

    /// <summary>Gets or initialises the customer-facing notes; null to omit.</summary>
    public string? CustomerNotes { get; init; }

    /// <summary>Gets or initialises the private admin notes; null to omit.</summary>
    public string? AdminNotes { get; init; }

    /// <summary>Gets or initialises the list of line items.</summary>
    public required IReadOnlyList<CreateQuoteItemRequest> Items { get; init; }
}

/// <summary>A single line item in a create-quote request.</summary>
public sealed class CreateQuoteItemRequest
{
    /// <summary>Gets or initialises the number of units.</summary>
    public required int Quantity { get; init; }

    /// <summary>Gets or initialises the human-readable description.</summary>
    public required string Description { get; init; }

    /// <summary>Gets or initialises the price per unit.</summary>
    public required decimal UnitPrice { get; init; }

    /// <summary>Gets or initialises the discount percentage (0–100).</summary>
    public decimal DiscountPercent { get; init; }

    /// <summary>Gets or initialises whether this item is subject to tax.</summary>
    public bool Taxed { get; init; }
>>>>>>> origin/main
}
