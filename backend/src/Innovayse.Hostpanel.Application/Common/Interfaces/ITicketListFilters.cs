using Innovayse.Hostpanel.Domain.Support.Enums;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// The ticket filters every ticket list shares: the status, the priority and the department. A list query that
/// offers them implements this next to <see cref="IListQuery"/>, so the
/// validation (<see cref="Innovayse.Hostpanel.Application.Support.Common.TicketListQueryValidator{T}"/>) and the SQL
/// (<see cref="Innovayse.Hostpanel.Application.Support.Extensions.TicketFilterExtensions"/>) are written once.
/// </summary>
public interface ITicketListFilters
{
    /// <summary>Gets the lifecycle status to keep; null keeps every status.</summary>
    public TicketStatus? Status { get; }

    /// <summary>Gets the priority to keep; null keeps every priority.</summary>
    public TicketPriority? Priority { get; }

    /// <summary>Gets the id of the department to keep; null keeps every department, and a ticket with none is left out when one is named.</summary>
    public int? DepartmentId { get; }
}
