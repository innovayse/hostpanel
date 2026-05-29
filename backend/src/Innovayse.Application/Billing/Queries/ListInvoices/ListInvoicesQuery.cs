namespace Innovayse.Application.Billing.Queries.ListInvoices;

/// <summary>Paginated, optionally filtered query for all invoices — admin view.</summary>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Number of items per page (max 100).</param>
/// <param name="Status">Optional status filter (e.g. Paid, Unpaid, Overdue, Draft, etc.).</param>
/// <param name="From">Optional start date filter (inclusive, UTC).</param>
/// <param name="To">Optional end date filter (inclusive, UTC).</param>
public record ListInvoicesQuery(
    int Page,
    int PageSize,
    string? Status = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null);
