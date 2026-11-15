using Innovayse.Hostpanel.Application.Billing.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Interfaces;

namespace Innovayse.Hostpanel.Application.Billing.Queries.ListGatewayLogs;

/// <summary>Paginated, filtered query for the payment-gateway log for the admin view.</summary>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Items per page (1 to 100); a size outside that range is refused, not clamped.</param>
/// <param name="Search">
/// Optional text; only entries whose gateway or debug data (invoice, reference, notes) contain it (case-insensitive, a
/// <c>%</c> or <c>_</c> matches itself) are listed. Blank lists all.
/// </param>
/// <param name="From">Optional first day kept (UTC), bound from <c>yyyy-MM-dd</c>; <see langword="null"/> for no lower bound.</param>
/// <param name="To">Optional last day kept (UTC), bound from <c>yyyy-MM-dd</c>, the whole day included; <see langword="null"/> for no upper bound.</param>
/// <param name="Result">Optional outcome filter, bound by name from the query string; <see langword="null"/> for every outcome.</param>
public sealed record ListGatewayLogsQuery(
    int Page,
    int PageSize,
    string? Search = null,
    DateOnly? From = null,
    DateOnly? To = null,
    GatewayLogResult? Result = null) : IListQuery, IPeriodListFilter;
