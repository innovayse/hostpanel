namespace Innovayse.Application.Billing.Commands.DuplicateQuote;

/// <summary>Command to duplicate a quote as a new draft.</summary>
/// <param name="QuoteId">The quote to duplicate.</param>
public record DuplicateQuoteCommand(int QuoteId);
