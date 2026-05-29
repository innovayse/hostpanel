namespace Innovayse.Application.Support.Queries.ListTickets;

using Innovayse.Application.Common;
using Innovayse.Application.Support.DTOs;
using Innovayse.Domain.Support;
using Innovayse.Domain.Support.Interfaces;

/// <summary>
/// Returns a paged list of support tickets, optionally filtered by status or search term.
/// Maps results to <see cref="TicketListItemDto"/>.
/// </summary>
public sealed class ListTicketsHandler(ITicketRepository repo, IDepartmentRepository departmentRepo)
{
    /// <summary>
    /// Handles <see cref="ListTicketsQuery"/>.
    /// </summary>
    /// <param name="query">The list tickets query with optional filters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A paged result containing ticket summary DTOs.</returns>
    public async Task<PagedResult<TicketListItemDto>> HandleAsync(ListTicketsQuery query, CancellationToken ct)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var flaggedOnly = string.Equals(query.Status, "flagged", StringComparison.OrdinalIgnoreCase);
        TicketStatus? statusFilter = null;

        if (!flaggedOnly && !string.IsNullOrWhiteSpace(query.Status) &&
            Enum.TryParse<TicketStatus>(query.Status, true, out var parsed))
        {
            statusFilter = parsed;
        }

        var (tickets, totalCount) = await repo.ListAsync(page, pageSize, statusFilter, query.Search, flaggedOnly, ct);

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
