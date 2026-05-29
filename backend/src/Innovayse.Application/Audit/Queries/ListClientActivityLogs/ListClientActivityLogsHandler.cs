namespace Innovayse.Application.Audit.Queries.ListClientActivityLogs;

using Innovayse.Application.Audit.DTOs;
using Innovayse.Application.Common;
using Innovayse.Domain.Audit.Interfaces;

/// <summary>Returns a paged, filtered list of activity log entries for a specific client.</summary>
/// <param name="repo">Activity log repository.</param>
public sealed class ListClientActivityLogsHandler(IActivityLogRepository repo)
{
    /// <summary>Handles <see cref="ListClientActivityLogsQuery"/>.</summary>
    /// <param name="query">The query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paged result of activity log DTOs.</returns>
    public async Task<PagedResult<ActivityLogDto>> HandleAsync(ListClientActivityLogsQuery query, CancellationToken ct)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var (items, total) = await repo.ListByClientIdAsync(
            query.ClientId, page, pageSize,
            query.Date, query.AdminSearch, query.Description, query.IpAddress,
            ct);

        var dtos = items
            .Select(l => new ActivityLogDto(l.Id, l.Description, l.AdminName, l.AdminEmail, l.IpAddress, l.CreatedAt))
            .ToList();

        return new PagedResult<ActivityLogDto>(dtos, total, page, pageSize);
    }
}
