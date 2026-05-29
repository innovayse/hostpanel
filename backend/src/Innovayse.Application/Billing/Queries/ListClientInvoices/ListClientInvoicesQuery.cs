namespace Innovayse.Application.Billing.Queries.ListClientInvoices;

using Innovayse.Domain.Billing;

/// <summary>Paginated query for a specific client's invoices with optional filters.</summary>
/// <param name="ClientId">The client's primary key.</param>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Number of items per page (max 100).</param>
/// <param name="Status">Optional status filter; null returns all statuses.</param>
/// <param name="From">Optional start date filter (inclusive, UTC); null for no lower bound.</param>
/// <param name="To">Optional end date filter (inclusive, UTC); null for no upper bound.</param>
public record ListClientInvoicesQuery(
    int ClientId,
    int Page,
    int PageSize,
    InvoiceStatus? Status = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null);
