namespace Innovayse.Application.Billing.Queries.GetQuote;

<<<<<<< HEAD
/// <summary>Query to fetch a single quote by ID.</summary>
/// <param name="Id">The quote ID.</param>
public sealed record GetQuoteQuery(int Id);
=======
/// <summary>Query to retrieve a single quote by ID with its line items.</summary>
/// <param name="QuoteId">The quote's primary key.</param>
public record GetQuoteQuery(int QuoteId);
>>>>>>> origin/main
