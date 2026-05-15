namespace Innovayse.Domain.Support.Events;

using Innovayse.Domain.Common;

/// <summary>
/// Raised when a support ticket is closed.
/// Used to trigger a satisfaction survey or closing notification to the client.
/// </summary>
/// <param name="TicketId">The ID of the closed ticket.</param>
/// <param name="ClientId">The ID of the client who owns the ticket.</param>
public record TicketClosedEvent(int TicketId, int ClientId) : IDomainEvent;
