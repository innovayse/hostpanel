namespace Innovayse.Domain.Support.Events;

using Innovayse.Domain.Common;

/// <summary>
/// Raised when a new support ticket is successfully created.
/// Used to trigger notifications to the assigned department.
/// </summary>
/// <param name="TicketId">The ID of the newly created ticket.</param>
/// <param name="ClientId">The ID of the client who opened the ticket.</param>
/// <param name="DepartmentId">The ID of the department the ticket was routed to.</param>
public record TicketCreatedEvent(int TicketId, int ClientId, int DepartmentId) : IDomainEvent;
