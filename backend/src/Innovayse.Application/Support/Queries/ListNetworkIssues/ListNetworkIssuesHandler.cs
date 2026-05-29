namespace Innovayse.Application.Support.Queries.ListNetworkIssues;

using Innovayse.Application.Common;
using Innovayse.Application.Support.DTOs;
using Innovayse.Domain.Support;
using Innovayse.Domain.Support.Interfaces;

/// <summary>
/// Returns a paged list of network issues, optionally filtered by status.
/// Maps results to <see cref="NetworkIssueDto"/>.
/// </summary>
public sealed class ListNetworkIssuesHandler(INetworkIssueRepository repo)
{
    /// <summary>
    /// Handles <see cref="ListNetworkIssuesQuery"/>.
    /// </summary>
    /// <param name="query">The list network issues query with optional status filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A paged result containing network issue DTOs.</returns>
    public async Task<PagedResult<NetworkIssueDto>> HandleAsync(ListNetworkIssuesQuery query, CancellationToken ct)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        NetworkIssueStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(query.Status) &&
            Enum.TryParse<NetworkIssueStatus>(query.Status, true, out var parsed))
        {
            statusFilter = parsed;
        }

        var (issues, totalCount) = await repo.ListAsync(page, pageSize, statusFilter, ct);

        var items = issues
            .Select(n => new NetworkIssueDto(
                n.Id,
                n.Title,
                n.Type.ToString(),
                n.Server,
                n.Priority.ToString(),
                n.Status.ToString(),
                n.StartDate,
                n.EndDate,
                n.Description,
                n.CreatedAt))
            .ToList();

        return new PagedResult<NetworkIssueDto>(items, totalCount, page, pageSize);
    }
}
