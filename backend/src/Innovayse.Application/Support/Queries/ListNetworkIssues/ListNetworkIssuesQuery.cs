namespace Innovayse.Application.Support.Queries.ListNetworkIssues;

/// <summary>Query to retrieve a paged, optionally filtered list of network issues.</summary>
/// <param name="Page">One-based page number.</param>
/// <param name="PageSize">Number of items per page.</param>
/// <param name="Status">Optional status filter (e.g. "Reported", "Investigating", "Resolved").</param>
public record ListNetworkIssuesQuery(int Page, int PageSize, string? Status = null);
