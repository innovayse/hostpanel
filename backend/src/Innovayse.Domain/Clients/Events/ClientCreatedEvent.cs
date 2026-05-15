namespace Innovayse.Domain.Clients.Events;

using Innovayse.Domain.Common;

/// <summary>
/// Domain event raised when a new <see cref="Client"/> is created.
/// Dispatched by Wolverine after the aggregate is persisted.
/// </summary>
/// <param name="ClientId">The new client's ID.</param>
/// <param name="UserId">The linked Identity user ID.</param>
/// <param name="Email">The client's email address.</param>
public record ClientCreatedEvent(int ClientId, string UserId, string Email) : IDomainEvent;
