namespace Innovayse.Domain.Services.Events;

using Innovayse.Domain.Common;

/// <summary>Raised when a client service is terminated.</summary>
/// <param name="ServiceId">The terminated service ID.</param>
/// <param name="ClientId">The owning client ID.</param>
public record ClientServiceTerminatedEvent(int ServiceId, int ClientId) : IDomainEvent;
