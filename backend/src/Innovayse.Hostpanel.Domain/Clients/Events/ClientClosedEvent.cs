using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Clients.Events;

/// <summary>Domain event raised when a <see cref="Client"/> is closed for good; its sessions, services and open renewal invoices are settled by the handlers of this event.</summary>
/// <param name="ClientId">The closed client's ID.</param>
/// <param name="UserId">The identity subject of the account owner, whose sessions end.</param>
/// <param name="OccurredAt">When the change was made, UTC, as the caller of the domain method read it.</param>
public record ClientClosedEvent(int ClientId, string UserId, DateTimeOffset OccurredAt) : IDomainEvent;
