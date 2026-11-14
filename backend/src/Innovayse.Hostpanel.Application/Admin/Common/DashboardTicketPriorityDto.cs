using Innovayse.Hostpanel.Domain.Support.Enums;

namespace Innovayse.Hostpanel.Application.Admin.Common;

/// <summary>The open tickets of one priority.</summary>
/// <param name="Priority">The priority.</param>
/// <param name="Count">How many open tickets have it, zero when none do.</param>
public sealed record DashboardTicketPriorityDto(TicketPriority Priority, int Count);
