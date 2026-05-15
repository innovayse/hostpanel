namespace Innovayse.Domain.Services.Events;

using Innovayse.Domain.Common;

/// <summary>Raised when a client service is suspended.</summary>
/// <param name="ServiceId">The suspended service ID.</param>
/// <param name="ClientId">The owning client ID.</param>
public record ClientServiceSuspendedEvent(int ServiceId, int ClientId) : IDomainEvent;
