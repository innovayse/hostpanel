using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Clients.Events;

/// <summary>
/// Domain event raised when a new <see cref="Client"/> is created.
/// Dispatched by Wolverine after the aggregate is persisted.
/// </summary>
/// <param name="ClientId">The new client's ID.</param>
/// <param name="UserId">The linked Identity user ID.</param>
/// <param name="Email">The client's email address.</param>
/// <param name="OccurredAt">When the change was made, UTC, as the caller of the domain method read it.</param>
public record ClientCreatedEvent(int ClientId, string UserId, string Email, DateTimeOffset OccurredAt) : IDomainEvent;
