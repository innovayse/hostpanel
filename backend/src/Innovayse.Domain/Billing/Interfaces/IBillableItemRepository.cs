namespace Innovayse.Domain.Billing.Interfaces;

/// <summary>
/// Persistence contract for the <see cref="BillableItem"/> aggregate.
/// Implemented in Infrastructure by EF Core.
/// </summary>
public interface IBillableItemRepository
{
    /// <summary>
    /// Finds a billable item by primary key.
    /// </summary>
    /// <param name="id">BillableItem primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The billable item, or <see langword="null"/> if not found.</returns>
    Task<BillableItem?> FindByIdAsync(int id, CancellationToken ct);

    /// <summary>
    /// Returns a paginated list of billable items.
    /// </summary>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Number of items per page (max 100).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Tuple of items for the current page and total matching count.</returns>
    Task<(IReadOnlyList<BillableItem> Items, int TotalCount)> ListAsync(int page, int pageSize, CancellationToken ct);

    /// <summary>
    /// Returns uninvoiced billable items for a specific client.
    /// </summary>
    /// <param name="clientId">The client's primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>All uninvoiced items for the client.</returns>
    Task<IReadOnlyList<BillableItem>> ListUninvoicedAsync(int clientId, CancellationToken ct);

    /// <summary>
    /// Returns recurring billable items for a specific client.
    /// </summary>
    /// <param name="clientId">The client's primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>All recurring items for the client.</returns>
    Task<IReadOnlyList<BillableItem>> ListRecurringAsync(int clientId, CancellationToken ct);

    /// <summary>
    /// Returns a paginated list of invoiced billable items for a specific client.
    /// </summary>
    /// <param name="clientId">The client's primary key.</param>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Tuple of items for the current page and total matching count.</returns>
    Task<(IReadOnlyList<BillableItem> Items, int TotalCount)> ListInvoicedByClientAsync(
        int clientId, int page, int pageSize, CancellationToken ct);

    /// <summary>
    /// Returns all billable items due for cron-triggered invoicing.
    /// </summary>
    Task<IReadOnlyList<BillableItem>> GetDueForCronInvoicingAsync(CancellationToken ct);

    /// <summary>
    /// Returns all recurring billable items whose next due date has arrived.
    /// </summary>
    Task<IReadOnlyList<BillableItem>> GetDueForRecurrenceAsync(CancellationToken ct);

    /// <summary>
    /// Finds multiple billable items by their IDs.
    /// </summary>
    Task<IReadOnlyList<BillableItem>> FindByIdsAsync(IReadOnlyList<int> ids, CancellationToken ct);

    /// <summary>
    /// Adds multiple new billable items to the repository.
    /// </summary>
    void AddRange(IEnumerable<BillableItem> items);

    /// <summary>
    /// Adds a new billable item to the repository.
    /// </summary>
    /// <param name="item">The new billable item aggregate.</param>
    void Add(BillableItem item);

    /// <summary>
    /// Deletes a billable item from the repository.
    /// </summary>
    /// <param name="item">The billable item to delete.</param>
    void Delete(BillableItem item);
}
