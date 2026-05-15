namespace Innovayse.Domain.Billing.Interfaces;

/// <summary>
/// Persistence contract for the <see cref="Invoice"/> aggregate.
/// Implemented in Infrastructure by EF Core.
/// </summary>
public interface IInvoiceRepository
{
    /// <summary>
    /// Finds an invoice by primary key, including its line items.
    /// </summary>
    /// <param name="id">Invoice primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The invoice with items loaded, or <see langword="null"/> if not found.</returns>
    Task<Invoice?> FindByIdAsync(int id, CancellationToken ct);

    /// <summary>
    /// Returns a paginated list of all invoices (admin view).
    /// </summary>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Number of items per page (max 100).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Tuple of items for the current page and total matching count.</returns>
    Task<(IReadOnlyList<Invoice> Items, int TotalCount)> ListAsync(int page, int pageSize, CancellationToken ct);

    /// <summary>
    /// Returns all invoices for a specific client, ordered newest first.
    /// </summary>
    /// <param name="clientId">The client's primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>All invoices for the client, with items loaded.</returns>
    Task<IReadOnlyList<Invoice>> ListByClientAsync(int clientId, CancellationToken ct);

    /// <summary>
    /// Adds a new invoice to the repository.
    /// Call <c>SaveChangesAsync</c> after adding to persist.
    /// </summary>
    /// <param name="invoice">The new invoice aggregate.</param>
    void Add(Invoice invoice);

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
    /// Returns all invoices regardless of status, without pagination.
    /// Used for aggregate stats calculations.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>All invoices in the system.</returns>
    Task<IReadOnlyList<Invoice>> GetAllAsync(CancellationToken ct);
}
