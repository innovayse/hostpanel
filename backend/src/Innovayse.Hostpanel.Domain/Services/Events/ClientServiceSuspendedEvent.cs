using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Services.Events;

/// <summary>Raised when a client service is suspended.</summary>
/// <param name="ServiceId">The suspended service ID.</param>
/// <param name="ClientId">The owning client ID.</param>
/// <param name="OccurredAt">When the change was made, UTC, as the caller of the domain method read it.</param>
public record ClientServiceSuspendedEvent(int ServiceId, int ClientId, DateTimeOffset OccurredAt) : IDomainEvent;
