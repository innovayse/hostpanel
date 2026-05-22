namespace Innovayse.Application.Billing.Queries.ListClientTransactions;

/// <summary>Query to list a client's transactions with financial summary.</summary>
/// <param name="ClientId">The client's primary key.</param>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Number of items per page.</param>
public record ListClientTransactionsQuery(int ClientId, int Page, int PageSize);
