using Innovayse.Hostpanel.Application.Common.Interfaces;

namespace Innovayse.Hostpanel.Application.Notifications.Queries.ListEmailLogs;

/// <summary>Query to retrieve a page of the platform's email log, narrowed by a search and filters.</summary>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Items per page (1 to 100); a size outside that range is refused, not clamped.</param>
/// <param name="Search">Optional text; only entries whose subject or recipient contain it (case-insensitive) are listed. Blank lists all.</param>
/// <param name="Success">Optional outcome: true for delivered mail, false for failed mail; <see langword="null"/> for both.</param>
/// <param name="From">Optional first day kept (UTC), bound from <c>yyyy-MM-dd</c>; <see langword="null"/> for no lower bound.</param>
/// <param name="To">Optional last day kept (UTC), bound from <c>yyyy-MM-dd</c>, the whole day included; <see langword="null"/> for no upper bound.</param>
public sealed record ListEmailLogsQuery(
    int Page,
    int PageSize,
    string? Search = null,
    bool? Success = null,
    DateOnly? From = null,
    DateOnly? To = null) : IListQuery, IEmailLogListFilters;
