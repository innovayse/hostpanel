namespace Innovayse.Domain.Billing.Interfaces;

/// <summary>
/// Persistence contract for the <see cref="Quote"/> aggregate.
/// Implemented in Infrastructure by EF Core.
/// </summary>
public interface IQuoteRepository
{
    /// <summary>
    /// Returns a paginated list of quotes for a specific client.
    /// </summary>
    /// <param name="clientId">The client's primary key.</param>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Number of items per page (max 100).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Tuple of items for the current page and total matching count.</returns>
    Task<(IReadOnlyList<Quote> Items, int TotalCount)> ListByClientAsync(int clientId, int page, int pageSize, CancellationToken ct);

    /// <summary>
    /// Finds a quote by primary key, including its line items.
    /// </summary>
    /// <param name="id">Quote primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The quote with items loaded, or <see langword="null"/> if not found.</returns>
    Task<Quote?> FindByIdAsync(int id, CancellationToken ct);

    /// <summary>
    /// Finds multiple quotes by their IDs, including items.
    /// </summary>
    /// <param name="ids">The quote IDs to find.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Quotes matching the provided IDs.</returns>
    Task<IReadOnlyList<Quote>> FindByIdsAsync(IReadOnlyList<int> ids, CancellationToken ct);

    /// <summary>
    /// Adds a new quote to the repository.
    /// Call <c>SaveChangesAsync</c> after adding to persist.
    /// </summary>
    /// <param name="quote">The new quote aggregate.</param>
    void Add(Quote quote);

    /// <summary>
    /// Removes a quote from the repository.
    /// Call <c>SaveChangesAsync</c> after removing to persist.
    /// </summary>
    /// <param name="quote">The quote to remove.</param>
    void Remove(Quote quote);
}
