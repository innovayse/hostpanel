namespace Innovayse.Application.Billing.Queries.GetTransaction;

/// <summary>
/// Retrieves a single transaction by ID.
/// </summary>
/// <param name="Id">Transaction ID.</param>
public sealed record GetTransactionByIdQuery(int Id);
