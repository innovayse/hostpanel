using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Support.Enums;

namespace Innovayse.Hostpanel.Application.Support.Queries.ListClientTickets;

/// <summary>Query to list a client's tickets with pagination, an optional search and optional filters.</summary>
/// <param name="ClientId">FK to the client.</param>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Items per page (1 to 100); a size outside that range is refused, not clamped.</param>
/// <param name="Search">Optional text; only tickets whose subject contains it (case-insensitive) or whose id it is are listed. Blank lists all.</param>
/// <param name="Status">Optional status filter, bound by name from the query string; <see langword="null"/> for every status.</param>
/// <param name="Priority">Optional priority filter, bound by name from the query string; <see langword="null"/> for every priority.</param>
/// <param name="DepartmentId">Optional department filter; <see langword="null"/> for every department.</param>
public sealed record ListClientTicketsQuery(
    int ClientId,
    int Page,
    int PageSize,
    string? Search = null,
    TicketStatus? Status = null,
    TicketPriority? Priority = null,
    int? DepartmentId = null) : IListQuery, ITicketListFilters;
