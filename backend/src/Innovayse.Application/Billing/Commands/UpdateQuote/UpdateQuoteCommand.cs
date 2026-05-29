namespace Innovayse.Application.Billing.Commands.UpdateQuote;

using Innovayse.Domain.Billing;

/// <summary>Command to update an existing quote's details and line items.</summary>
/// <param name="QuoteId">The quote to update.</param>
/// <param name="Subject">New quote subject / title.</param>
/// <param name="Status">New lifecycle status.</param>
/// <param name="ExpiryDate">New expiry date (UTC).</param>
/// <param name="Notes">Optional notes or terms; null to clear.</param>
/// <param name="Items">Full list of item changes (add, update, or delete).</param>
public record UpdateQuoteCommand(
    int QuoteId,
    string Subject,
    QuoteStatus Status,
    DateTimeOffset ExpiryDate,
    string? Notes,
    IReadOnlyList<UpdateQuoteItemEntry> Items);

/// <summary>
/// A single line item entry for updating a quote.
/// When <paramref name="Id"/> is null, a new item is created.
/// When <paramref name="IsDeleted"/> is true, the item is removed.
/// </summary>
/// <param name="Id">Existing item ID; null for new items.</param>
/// <param name="Description">Human-readable description.</param>
/// <param name="UnitPrice">Price per unit (>= 0).</param>
/// <param name="Quantity">Number of units (>= 1).</param>
/// <param name="IsDeleted">When true, the item with this ID will be removed.</param>
public record UpdateQuoteItemEntry(
    int? Id,
    string Description,
    decimal UnitPrice,
    int Quantity,
    bool IsDeleted = false);
