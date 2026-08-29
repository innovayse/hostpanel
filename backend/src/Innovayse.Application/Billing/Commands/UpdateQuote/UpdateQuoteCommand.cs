namespace Innovayse.Application.Billing.Commands.UpdateQuote;

using Innovayse.Domain.Billing;

/// <summary>Command to update an existing quote's details and line items.</summary>
/// <param name="QuoteId">The quote to update.</param>
/// <param name="Subject">New quote subject / title.</param>
/// <param name="Stage">New lifecycle stage.</param>
/// <param name="ValidUntil">New expiry date (UTC).</param>
/// <param name="Notes">Optional notes or terms; null to clear.</param>
/// <param name="ProposalText">Optional proposal text; null to clear.</param>
/// <param name="CustomerNotes">Optional customer-facing notes; null to clear.</param>
/// <param name="AdminNotes">Optional admin-only notes; null to clear.</param>
/// <param name="Items">Full list of item changes (add, update, or delete).</param>
public record UpdateQuoteCommand(
    int QuoteId,
    string Subject,
    QuoteStage Stage,
    DateTimeOffset ValidUntil,
    string? Notes,
    string? ProposalText,
    string? CustomerNotes,
    string? AdminNotes,
    IReadOnlyList<UpdateQuoteItemEntry> Items);
