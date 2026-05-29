namespace Innovayse.Application.Billing.Commands.ConvertQuoteToInvoice;

/// <summary>Command to convert a quote into a draft invoice.</summary>
/// <param name="QuoteId">The quote to convert.</param>
public record ConvertQuoteToInvoiceCommand(int QuoteId);
