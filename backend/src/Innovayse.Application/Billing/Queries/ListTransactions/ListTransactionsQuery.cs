namespace Innovayse.Application.Billing.Queries.ListTransactions;

/// <summary>Query to list a client's transactions with financial summary.</summary>
/// <param name="ClientId">The client's primary key.</param>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Number of items per page.</param>
public record ListTransactionsQuery(int ClientId, int Page, int PageSize);
