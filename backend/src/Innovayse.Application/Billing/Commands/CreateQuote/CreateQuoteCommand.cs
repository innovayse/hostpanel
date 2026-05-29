namespace Innovayse.Application.Billing.Commands.CreateQuote;

using Innovayse.Domain.Billing;

/// <summary>Command to create a new quote for a client.</summary>
/// <param name="ClientId">FK to the client receiving the quote.</param>
/// <param name="Subject">Quote subject / title.</param>
/// <param name="Stage">Initial lifecycle stage.</param>
/// <param name="ValidUntil">Expiry date; null for no expiry.</param>
/// <param name="ProposalText">Proposal text displayed at top; null to omit.</param>
/// <param name="CustomerNotes">Customer-facing footer notes; null to omit.</param>
/// <param name="AdminNotes">Private admin notes; null to omit.</param>
/// <param name="Items">Line items to attach to the quote.</param>
public record CreateQuoteCommand(
    int ClientId,
    string Subject,
    QuoteStage Stage,
    DateTimeOffset? ValidUntil,
    string? ProposalText,
    string? CustomerNotes,
    string? AdminNotes,
    IReadOnlyList<QuoteItemRequest> Items);

/// <summary>Request data for a single quote line item.</summary>
/// <param name="Quantity">Number of units (>= 1).</param>
/// <param name="Description">Human-readable description.</param>
/// <param name="UnitPrice">Price per unit (>= 0).</param>
/// <param name="DiscountPercent">Discount percentage (0–100).</param>
/// <param name="Taxed">Whether the item is subject to tax.</param>
public record QuoteItemRequest(int Quantity, string Description, decimal UnitPrice, decimal DiscountPercent, bool Taxed);
