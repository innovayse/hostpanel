namespace Innovayse.Domain.Billing.Interfaces;

/// <summary>
/// Persistence contract for the <see cref="Quote"/> aggregate.
/// Implemented in Infrastructure by EF Core.
/// </summary>
public interface IQuoteRepository
{
    /// <summary>
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
    /// Adds a new quote to the repository.
    /// </summary>
    /// <param name="quote">The new quote aggregate.</param>
    void Add(Quote quote);

    /// <summary>
    /// Deletes a quote from the repository.
    /// </summary>
    /// <param name="quote">The quote to delete.</param>
    void Delete(Quote quote);
}
