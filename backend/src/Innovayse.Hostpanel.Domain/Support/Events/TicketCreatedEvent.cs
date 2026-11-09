using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Support.Events;

/// <summary>
/// Raised when a new support ticket is successfully created.
/// Used to trigger notifications to the assigned department.
/// </summary>
/// <param name="TicketId">The ID of the newly created ticket.</param>
/// <param name="ClientId">The ID of the client who opened the ticket.</param>
/// <param name="DepartmentId">The ID of the department the ticket was routed to.</param>
public record TicketCreatedEvent(int TicketId, int ClientId, int DepartmentId) : IDomainEvent
{
    /// <summary>
    /// When the change was made, UTC, given by whoever raised the event.
    /// </summary>
    public required DateTimeOffset OccurredAt { get; init; }
}
