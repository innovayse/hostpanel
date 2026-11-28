using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Application.Billing.Queries.ListInvoices;

/// <summary>Paginated, searchable, filterable query for all invoices: admin view.</summary>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Items per page (1 to 100); a size outside that range is refused, not clamped.</param>
/// <param name="Search">
/// Optional text; only invoices whose id or payment method contains it, or that belong to a client whose first name, last name,
/// company, sign-in address or account name contains it (case-insensitive, a <c>%</c> or <c>_</c> matches itself), are listed.
/// Blank lists all.
/// </param>
/// <param name="ClientId">Optional client filter; <see langword="null"/> for every client.</param>
/// <param name="Status">Optional status filter, bound by name from the query string; <see langword="null"/> for every status.</param>
/// <param name="From">Optional first creation day (UTC), bound from <c>yyyy-MM-dd</c>; <see langword="null"/> for no lower bound.</param>
/// <param name="To">Optional last creation day (UTC), bound from <c>yyyy-MM-dd</c>, the whole day included; <see langword="null"/> for no upper bound.</param>
public sealed record ListInvoicesQuery(
    int Page,
    int PageSize,
    string? Search = null,
    int? ClientId = null,
    InvoiceStatus? Status = null,
    DateOnly? From = null,
    DateOnly? To = null) : IListQuery, IInvoiceListFilters;
