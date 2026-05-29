namespace Innovayse.Domain.Billing.Interfaces;

/// <summary>
/// Persistence contract for the <see cref="Transaction"/> aggregate.
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
    /// Returns a paginated list of all transactions (admin view).
    /// </summary>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Number of items per page (max 100).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Tuple of items for the current page and total matching count.</returns>
    Task<(IReadOnlyList<Transaction> Items, int TotalCount)> ListAsync(int page, int pageSize, CancellationToken ct);

    /// <summary>
    /// Returns all transactions for a specific client.
    /// </summary>
    /// <param name="clientId">The client's primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>All transactions for the client, ordered newest first.</returns>
    Task<IReadOnlyList<Transaction>> ListByClientAsync(int clientId, CancellationToken ct);

    /// <summary>
    /// Adds a new transaction to the repository.
    /// Call <c>SaveChangesAsync</c> after adding to persist.
    /// </summary>
    /// <param name="transaction">The new transaction aggregate.</param>
    void Add(Transaction transaction);
}
