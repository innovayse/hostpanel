namespace Innovayse.Application.Billing.Commands.DeleteQuote;

/// <summary>Command to permanently delete a quote.</summary>
/// <param name="QuoteId">The quote to delete.</param>
public record DeleteQuoteCommand(int QuoteId);
