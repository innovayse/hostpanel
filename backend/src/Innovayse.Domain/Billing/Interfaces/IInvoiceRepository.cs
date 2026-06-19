namespace Innovayse.Domain.Billing.Interfaces;

/// <summary>
/// Persistence contract for the <see cref="Invoice"/> aggregate.
/// Implemented in Infrastructure by EF Core.
/// </summary>
public interface IInvoiceRepository
{
    /// <summary>
    /// Finds an invoice by primary key, including its line items and transactions.
    /// </summary>
    /// <param name="id">Invoice primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The invoice with items and transactions loaded, or <see langword="null"/> if not found.</returns>
    Task<Invoice?> FindByIdAsync(int id, CancellationToken ct);

    /// <summary>
    /// Returns a paginated list of all invoices (admin view), optionally filtered by status.
    /// </summary>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Number of items per page (max 100).</param>
    /// <param name="status">Optional status filter (null for all statuses).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Tuple of items for the current page and total matching count.</returns>
    Task<(IReadOnlyList<Invoice> Items, int TotalCount)> ListAsync(int page, int pageSize, string? status, CancellationToken ct);

    /// <summary>
    /// Returns a paginated, filtered list of all invoices (admin view).
    /// </summary>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Number of items per page (max 100).</param>
    /// <param name="status">Optional status filter; null returns all statuses.</param>
    /// <param name="from">Optional start date filter (inclusive); null for no lower bound.</param>
    /// <param name="to">Optional end date filter (inclusive); null for no upper bound.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Tuple of items for the current page and total matching count.</returns>
    Task<(IReadOnlyList<Invoice> Items, int TotalCount)> ListAsync(
        int page, int pageSize, InvoiceStatus? status,
        DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct);

    /// <summary>
    /// Returns all invoices for a specific client, ordered newest first.
    /// </summary>
    /// <param name="clientId">The client's primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>All invoices for the client, with items loaded.</returns>
    Task<IReadOnlyList<Invoice>> ListByClientAsync(int clientId, CancellationToken ct);

    /// <summary>
    /// Returns a paginated, filtered list of invoices for a specific client.
    /// </summary>
    /// <param name="clientId">The client's primary key.</param>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Number of items per page (max 100).</param>
    /// <param name="status">Optional status filter; null returns all statuses.</param>
    /// <param name="from">Optional start date filter (inclusive); null for no lower bound.</param>
    /// <param name="to">Optional end date filter (inclusive); null for no upper bound.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Tuple of items for the current page and total matching count.</returns>
    Task<(IReadOnlyList<Invoice> Items, int TotalCount)> ListByClientAsync(
        int clientId, int page, int pageSize, InvoiceStatus? status,
        DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct);

    /// <summary>
    /// Finds multiple invoices by their IDs, including items and transactions.
    /// </summary>
    /// <param name="ids">The invoice IDs to find.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Invoices matching the provided IDs.</returns>
    Task<IReadOnlyList<Invoice>> FindByIdsAsync(IReadOnlyList<int> ids, CancellationToken ct);

    /// <summary>
    /// Adds a new invoice to the repository.
    /// Call <c>SaveChangesAsync</c> after adding to persist.
    /// </summary>
    /// <param name="invoice">The new invoice aggregate.</param>
    void Add(Invoice invoice);

    /// <summary>
    /// Removes an invoice from the repository.
    /// Call <c>SaveChangesAsync</c> after removing to persist.
    /// </summary>
    /// <param name="invoice">The invoice to remove.</param>
    void Remove(Invoice invoice);

    /// <summary>
    /// Returns all paid invoices whose payment date falls within the given range.
    /// Used for revenue reports.
    /// </summary>
    /// <param name="start">Range start (inclusive, UTC).</param>
    /// <param name="end">Range end (inclusive, UTC).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paid invoices within the date range.</returns>
    Task<IReadOnlyList<Invoice>> GetPaidBetweenAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken ct);

    /// <summary>
    /// Returns all unpaid invoices whose due date has passed.
    /// Used by the overdue invoice cron to transition invoices to Overdue status.
    /// </summary>
    /// <param name="asOf">The reference point in time; invoices with DueDate before this are considered overdue.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Unpaid invoices whose DueDate is before <paramref name="asOf"/>.</returns>
    Task<IReadOnlyList<Invoice>> ListUnpaidOverdueAsync(DateTimeOffset asOf, CancellationToken ct);

    /// <summary>
    /// Returns all invoices regardless of status, without pagination.
    /// Used for aggregate stats calculations.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>All invoices in the system.</returns>
    Task<IReadOnlyList<Invoice>> GetAllAsync(CancellationToken ct);
}
