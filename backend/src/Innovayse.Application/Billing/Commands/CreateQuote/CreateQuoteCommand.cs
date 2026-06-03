namespace Innovayse.Application.Billing.Commands.CreateQuote;

/// <summary>Command to create a new quote with line items.</summary>
/// <param name="ClientId">FK to the client being quoted.</param>
/// <param name="Subject">Quote subject/title.</param>
/// <param name="ExpiryDate">Quote expiry date (UTC).</param>
/// <param name="Notes">Optional notes or terms.</param>
/// <param name="Items">Line items for the quote.</param>
/// <param name="ProposalText">Optional proposal text shown at the top.</param>
/// <param name="CustomerNotes">Optional customer-facing footer notes.</param>
/// <param name="AdminNotes">Optional admin-only internal notes.</param>
public sealed record CreateQuoteCommand(
    int ClientId,
    string Subject,
    DateTimeOffset ExpiryDate,
    string? Notes,
    IReadOnlyList<QuoteItemRequest> Items,
    string? ProposalText = null,
    string? CustomerNotes = null,
    string? AdminNotes = null);

/// <summary>A single line item request for a quote.</summary>
/// <param name="Description">Human-readable description.</param>
/// <param name="UnitPrice">Price per unit (≥ 0).</param>
/// <param name="Quantity">Number of units (≥ 1).</param>
/// <param name="DiscountPercent">Discount percentage (0–100). Defaults to 0.</param>
/// <param name="Taxed">Whether this item is taxed. Defaults to false.</param>
public sealed record QuoteItemRequest(string Description, decimal UnitPrice, int Quantity, decimal DiscountPercent = 0, bool Taxed = false);
