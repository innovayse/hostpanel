namespace Innovayse.Application.Support.Queries.ListClientTickets;

using Innovayse.Application.Common;
using Innovayse.Application.Support.DTOs;
using Innovayse.Domain.Support.Interfaces;

/// <summary>
/// Returns a paginated list of tickets for a specific client with department names resolved.
/// </summary>
public sealed class ListClientTicketsHandler(ITicketRepository repo, IDepartmentRepository departmentRepo)
{
    /// <summary>
    /// Handles <see cref="ListClientTicketsQuery"/>.
    /// </summary>
    /// <param name="query">The list client tickets query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A paged result containing ticket summary DTOs.</returns>
    public async Task<PagedResult<TicketListItemDto>> HandleAsync(ListClientTicketsQuery query, CancellationToken ct)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var (tickets, totalCount) = await repo.ListByClientIdAsync(query.ClientId, page, pageSize, query.Search, ct);

        var departments = await departmentRepo.ListAllAsync(ct);
        var deptMap = departments.ToDictionary(d => d.Id, d => d.Name);

        var items = tickets
            .Select(t => new TicketListItemDto(
                t.Id,
                t.Subject,
                t.Status.ToString(),
                t.Priority.ToString(),
                t.CreatedAt,
                t.Replies.Count,
                t.DepartmentId.HasValue && deptMap.TryGetValue(t.DepartmentId.Value, out var name) ? name : null,
                t.Replies.Count > 0 ? t.Replies.Max(r => r.CreatedAt) : null,
                t.IsFlagged,
                t.ClientId))
            .ToList();

        return new PagedResult<TicketListItemDto>(items, totalCount, page, pageSize);
    }
}
