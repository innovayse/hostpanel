namespace Innovayse.Application.Billing.Queries.ListClientQuotes;

/// <summary>Paginated query for a specific client's quotes.</summary>
/// <param name="ClientId">The client's primary key.</param>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Number of items per page (max 100).</param>
public record ListClientQuotesQuery(int ClientId, int Page, int PageSize);
