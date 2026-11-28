using Innovayse.Hostpanel.Application.Common.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Support;

namespace Innovayse.Hostpanel.Application.Support.Extensions;

/// <summary>
/// The one way a ticket list narrows its tickets: the search text on the subject and the ticket id, and the status, priority and
/// department filters, all in SQL ahead of ordering and paging, so the staff list and the client's list cannot differ.
/// </summary>
public static class TicketFilterExtensions
{
    /// <summary>Keeps the tickets the query's search text and filters name; a filter left empty keeps everything.</summary>
    /// <typeparam name="T">The ticket list query.</typeparam>
    /// <param name="tickets">The tickets to narrow.</param>
    /// <param name="query">The search text and the ticket filters.</param>
    /// <returns>The narrowed tickets.</returns>
    public static IQueryable<Ticket> Matching<T>(this IQueryable<Ticket> tickets, T query)
        where T : IListQuery, ITicketListFilters
    {
        ArgumentNullException.ThrowIfNull(tickets);
        ArgumentNullException.ThrowIfNull(query);

        tickets = tickets.SearchIncludingId(query.Search, t => t.Id, t => t.Subject);

        if (query.Status is { } status)
        {
            tickets = tickets.Where(t => t.Status == status);
        }

        if (query.Priority is { } priority)
        {
            tickets = tickets.Where(t => t.Priority == priority);
        }

        if (query.DepartmentId is { } departmentId)
        {
            tickets = tickets.Where(t => t.DepartmentId == departmentId);
        }

        return tickets;
    }
}
