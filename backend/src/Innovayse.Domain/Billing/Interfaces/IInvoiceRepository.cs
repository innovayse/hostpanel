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
    /// Returns every invoice of a client that carries at least one line charged to the given
    /// service, ordered newest first.
    /// </summary>
    /// <remarks>
    /// Scoped by client as well as by service, so a caller that has settled ownership once
    /// cannot be handed another account's invoice by a service id that slipped through. The two
    /// conditions are ANDed in SQL rather than the client check being left to the handler: a
    /// filter that lives in the query cannot be forgotten by the next caller of it.
    /// </remarks>
    /// <param name="clientId">The owning client's primary key.</param>
    /// <param name="clientServiceId">The service the lines must be charged to.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Matching invoices with items and transactions loaded, newest first.</returns>
    Task<IReadOnlyList<Invoice>> ListByClientServiceAsync(
        int clientId, int clientServiceId, CancellationToken ct);

    /// <summary>
    /// Returns how many of a client's invoices carry no service link on any line, and so cannot
    /// be attributed to any service at all.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This answers "is there billing history we cannot attribute?" without attributing any of
    /// it. Every invoice raised before <c>invoice_items.client_service_id</c> existed is in this
    /// count, as is every line the platform still has no service in hand for at the moment it is
    /// written — a one-off billable item, a domain charge, an admin adjustment, and the first
    /// payment on a new order, which is invoiced before the service it buys is created.
    /// </para>
    /// <para>
    /// The portal needs it to tell "this service was never charged" apart from "charges exist
    /// that were never recorded against a service, and none of them can honestly be shown here".
    /// Showing an empty table for the second case would state a fact the data does not support.
    /// </para>
    /// </remarks>
    /// <param name="clientId">The owning client's primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>How many of the client's invoices have no service link on any line.</returns>
    Task<int> CountUnattributedByClientAsync(int clientId, CancellationToken ct);

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

    /// <summary>Finds an invoice by its external system ID (for migration deduplication).</summary>
    Task<Invoice?> FindByExternalIdAsync(string externalId, CancellationToken ct);

    /// <summary>
    /// Lists unpaid/overdue invoices with an open hosted-gateway payment session
    /// whose attempt started inside the given window. Used by the reconciliation job.
    /// </summary>
    /// <param name="startedAfter">Only sessions started after this instant (exclusive lower bound).</param>
    /// <param name="startedBefore">Only sessions started before this instant (exclusive upper bound).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Invoices with a pending gateway session inside the window.</returns>
    Task<IReadOnlyList<Invoice>> ListPendingGatewayPaymentsAsync(
        DateTimeOffset startedAfter, DateTimeOffset startedBefore, CancellationToken ct);

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
