using Innovayse.Hostpanel.Application.Billing.Common.Enums;

namespace Innovayse.Hostpanel.Application.Billing.Queries.ListGatewayLogs;

/// <summary>Paginated, filtered query for the payment-gateway log - admin view.</summary>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Number of entries per page (max 100).</param>
/// <param name="From">Optional start of the date range (inclusive, UTC).</param>
/// <param name="To">Optional end of the date range (inclusive, UTC).</param>
/// <param name="Gateway">Optional gateway name filter (case-insensitive contains).</param>
/// <param name="DebugData">Optional text searched in the entry's debug data (case-insensitive contains).</param>
/// <param name="Result">Optional outcome filter.</param>
public sealed record ListGatewayLogsQuery(
    int Page = 1,
    int PageSize = 25,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    string? Gateway = null,
    string? DebugData = null,
    GatewayLogResult? Result = null);
