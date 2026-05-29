namespace Innovayse.Application.Billing.Queries.GetQuote;

/// <summary>Query to fetch a single quote by ID.</summary>
/// <param name="Id">The quote ID.</param>
public sealed record GetQuoteQuery(int Id);
