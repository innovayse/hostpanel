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
    /// <param name="id">Billable item primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The billable item, or <see langword="null"/> if not found.</returns>
    Task<BillableItem?> FindByIdAsync(int id, CancellationToken ct);

    /// <summary>
    /// Finds multiple billable items by their primary keys.
    /// </summary>
    /// <param name="ids">List of primary keys to look up.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>All found billable items (may be fewer than requested if some IDs don't exist).</returns>
    Task<IReadOnlyList<BillableItem>> FindByIdsAsync(IReadOnlyList<int> ids, CancellationToken ct);

    /// <summary>
    /// Returns all uninvoiced billable items for a specific client.
    /// </summary>
    /// <param name="clientId">The client's primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>All uninvoiced items for the client, ordered by due date.</returns>
    Task<IReadOnlyList<BillableItem>> ListUninvoicedByClientAsync(int clientId, CancellationToken ct);

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
    /// Returns all billable items due for cron-based invoicing.
    /// Includes items with InvoiceAction in (InvoiceOnNextCron, InvoiceForDueDate),
    /// InvoiceId IS NULL, and DueDate &lt;= UtcNow.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Items ready for cron invoicing.</returns>
    Task<IReadOnlyList<BillableItem>> GetDueForCronInvoicingAsync(CancellationToken ct);

    /// <summary>
    /// Returns all recurring billable items due for their next recurrence cycle.
    /// Includes items with InvoiceAction = Recur, DueDate &lt;= UtcNow,
    /// and (RecurrenceLimit IS NULL OR InvoiceCount &lt; RecurrenceLimit).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Recurring items ready for invoicing.</returns>
    Task<IReadOnlyList<BillableItem>> GetDueForRecurrenceAsync(CancellationToken ct);

    /// <summary>
    /// Adds a new billable item to the repository.
    /// Call <c>SaveChangesAsync</c> after adding to persist.
    /// </summary>
    /// <param name="item">The new billable item.</param>
    void Add(BillableItem item);

    /// <summary>
    /// Adds multiple billable items to the repository.
    /// Call <c>SaveChangesAsync</c> after adding to persist.
    /// </summary>
    /// <param name="items">The billable items to add.</param>
    void AddRange(IEnumerable<BillableItem> items);

    /// <summary>
    /// Removes a billable item from the repository.
    /// Call <c>SaveChangesAsync</c> after removing to persist.
    /// </summary>
    /// <param name="item">The billable item to remove.</param>
    void Remove(BillableItem item);
}
