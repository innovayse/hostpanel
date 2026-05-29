namespace Innovayse.Application.Billing.Commands.CreateQuote;

/// <summary>Command to create a new quote with line items.</summary>
/// <param name="ClientId">FK to the client being quoted.</param>
/// <param name="Subject">Quote subject/title.</param>
/// <param name="ExpiryDate">Quote expiry date (UTC).</param>
/// <param name="Notes">Optional notes or terms.</param>
/// <param name="Items">Line items for the quote.</param>
public sealed record CreateQuoteCommand(
    int ClientId,
    string Subject,
    DateTimeOffset ExpiryDate,
    string? Notes,
    IReadOnlyList<QuoteItemRequest> Items);

/// <summary>A single line item request for a quote.</summary>
/// <param name="Description">Human-readable description.</param>
/// <param name="UnitPrice">Price per unit (≥ 0).</param>
/// <param name="Quantity">Number of units (≥ 1).</param>
public sealed record QuoteItemRequest(string Description, decimal UnitPrice, int Quantity);
