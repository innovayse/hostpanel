using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Support.Enums;

namespace Innovayse.Hostpanel.Application.Support.Queries.ListNetworkIssues;

/// <summary>Query to retrieve a page of network issues, narrowed by a search and filters.</summary>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Items per page (1 to 100); a size outside that range is refused, not clamped.</param>
/// <param name="Search">Optional text; only issues whose title, server or description contain it (case-insensitive) are listed. Blank lists all.</param>
/// <param name="Status">Optional lifecycle status, bound by name from the query string; <see langword="null"/> for every status.</param>
/// <param name="Type">Optional issue type, bound by name from the query string; <see langword="null"/> for every type.</param>
/// <param name="Priority">Optional priority, bound by name from the query string; <see langword="null"/> for every priority.</param>
public sealed record ListNetworkIssuesQuery(
    int Page,
    int PageSize,
    string? Search = null,
    NetworkIssueStatus? Status = null,
    NetworkIssueType? Type = null,
    NetworkIssuePriority? Priority = null) : IListQuery;
