namespace Innovayse.Application.Billing.Queries.GetTransaction;

/// <summary>Query to fetch a single transaction by its primary key.</summary>
/// <param name="Id">Transaction primary key.</param>
public record GetTransactionByIdQuery(int Id);
