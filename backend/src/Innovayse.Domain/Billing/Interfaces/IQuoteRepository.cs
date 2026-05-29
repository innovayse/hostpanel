namespace Innovayse.Domain.Billing.Interfaces;

/// <summary>
/// Persistence contract for the <see cref="Quote"/> aggregate.
/// Implemented in Infrastructure by EF Core.
/// </summary>
public interface IQuoteRepository
{
    /// <summary>
<<<<<<< HEAD
    /// Finds a quote by primary key, including its line items.
    /// </summary>
    /// <param name="id">Quote primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The quote with items loaded, or <see langword="null"/> if not found.</returns>
    Task<Quote?> FindByIdAsync(int id, CancellationToken ct);

    /// <summary>
    /// Returns a paginated list of all quotes (admin view).
    /// </summary>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Number of items per page (max 100).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Tuple of items for the current page and total matching count.</returns>
    Task<(IReadOnlyList<Quote> Items, int TotalCount)> ListAsync(int page, int pageSize, CancellationToken ct);

    /// <summary>
    /// Returns all quotes for a specific client, ordered newest first.
    /// </summary>
    /// <param name="clientId">The client's primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>All quotes for the client, with items loaded.</returns>
    Task<IReadOnlyList<Quote>> ListByClientAsync(int clientId, CancellationToken ct);

    /// <summary>
=======
>>>>>>> origin/main
    /// Returns a paginated list of quotes for a specific client.
    /// </summary>
    /// <param name="clientId">The client's primary key.</param>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Number of items per page (max 100).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Tuple of items for the current page and total matching count.</returns>
    Task<(IReadOnlyList<Quote> Items, int TotalCount)> ListByClientAsync(int clientId, int page, int pageSize, CancellationToken ct);

    /// <summary>
<<<<<<< HEAD
    /// Adds a new quote to the repository.
=======
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
>>>>>>> origin/main
    /// </summary>
    /// <param name="quote">The new quote aggregate.</param>
    void Add(Quote quote);

    /// <summary>
<<<<<<< HEAD
    /// Deletes a quote from the repository.
    /// </summary>
    /// <param name="quote">The quote to delete.</param>
    void Delete(Quote quote);
=======
    /// Removes a quote from the repository.
    /// Call <c>SaveChangesAsync</c> after removing to persist.
    /// </summary>
    /// <param name="quote">The quote to remove.</param>
    void Remove(Quote quote);
>>>>>>> origin/main
}
