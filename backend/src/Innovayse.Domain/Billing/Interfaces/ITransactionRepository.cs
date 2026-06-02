namespace Innovayse.Domain.Billing.Interfaces;

/// <summary>
/// Persistence contract for <see cref="Transaction"/> entities.
/// Implemented in Infrastructure by EF Core.
/// </summary>
public interface ITransactionRepository
{
    /// <summary>
    /// Finds a transaction by primary key.
    /// </summary>
    /// <param name="id">Transaction primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The transaction, or <see langword="null"/> if not found.</returns>
    Task<Transaction?> FindByIdAsync(int id, CancellationToken ct);

    /// <summary>
    /// Returns a paginated list of transactions for a specific client, ordered newest first.
    /// </summary>
    /// <param name="clientId">The client's primary key.</param>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Tuple of items for the current page and total matching count.</returns>
    Task<(IReadOnlyList<Transaction> Items, int TotalCount)> ListByClientAsync(
        int clientId, int page, int pageSize, CancellationToken ct);

    /// <summary>Returns a paginated list of all transactions across all clients, ordered newest first.</summary>
    Task<(IReadOnlyList<Transaction> Items, int TotalCount)> ListAllAsync(
        int page, int pageSize, CancellationToken ct);

    /// <summary>
    /// Computes the aggregate financial summary for a client's transactions.
    /// </summary>
    /// <param name="clientId">The client's primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Tuple of total amounts in, out, and fees.</returns>
    Task<(decimal TotalIn, decimal TotalOut, decimal TotalFees)> GetClientSummaryAsync(
        int clientId, CancellationToken ct);

    /// <summary>
    /// Adds a new transaction to the repository.
    /// Call <c>SaveChangesAsync</c> after adding to persist.
    /// </summary>
    /// <param name="transaction">The new transaction.</param>
    void Add(Transaction transaction);

    /// <summary>
    /// Removes a transaction from the repository.
    /// Call <c>SaveChangesAsync</c> after removing to persist.
    /// </summary>
    /// <param name="transaction">The transaction to remove.</param>
    void Remove(Transaction transaction);
}
