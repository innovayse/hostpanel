namespace Innovayse.Application.Billing.Queries.ListQuotes;

/// <summary>Query to fetch a paginated list of all quotes.</summary>
/// <param name="Page">1-based page number (default 1).</param>
/// <param name="PageSize">Items per page (default 20, max 100).</param>
public sealed record ListQuotesQuery(int Page = 1, int PageSize = 20);
