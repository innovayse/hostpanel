using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Support.Events;

/// <summary>
/// Raised when a reply is added to an existing support ticket.
/// Used to trigger email notifications to the relevant party.
/// </summary>
/// <param name="TicketId">The ID of the ticket that received the reply.</param>
/// <param name="IsStaffReply">Whether the reply was posted by a staff member.</param>
public record TicketRepliedEvent(int TicketId, bool IsStaffReply) : IDomainEvent
{
    /// <summary>
    /// When the change was made, UTC, given by whoever raised the event.
    /// </summary>
    public required DateTimeOffset OccurredAt { get; init; }
}
