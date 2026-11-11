using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Support.Events;

/// <summary>
/// Raised when a support ticket is closed.
/// Used to trigger a satisfaction survey or closing notification to the client.
/// </summary>
/// <param name="TicketId">The ID of the closed ticket.</param>
/// <param name="ClientId">The ID of the client who owns the ticket.</param>
public record TicketClosedEvent(int TicketId, int ClientId) : IDomainEvent
{
    /// <summary>
    /// When the change was made, UTC, given by whoever raised the event.
    /// </summary>
    public required DateTimeOffset OccurredAt { get; init; }
}
