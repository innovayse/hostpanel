using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Application.Billing.Queries.ListClientInvoices;

/// <summary>Paginated query for a specific client's invoices with an optional search and optional filters.</summary>
/// <param name="ClientId">The client's primary key.</param>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Items per page (1 to 100); a size outside that range is refused, not clamped.</param>
/// <param name="Search">Optional text; only invoices whose id or payment method contains it (case-insensitive) are listed. Blank lists all.</param>
/// <param name="Status">Optional status filter, bound by name from the query string; <see langword="null"/> for every status.</param>
/// <param name="From">Optional first creation moment (inclusive, UTC); <see langword="null"/> for no lower bound.</param>
/// <param name="To">Optional last creation day (UTC), the whole day included; <see langword="null"/> for no upper bound.</param>
public sealed record ListClientInvoicesQuery(
    int ClientId,
    int Page,
    int PageSize,
    string? Search = null,
    InvoiceStatus? Status = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null) : IListQuery, IInvoiceListFilters;
