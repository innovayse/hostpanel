using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Services.Events;

/// <summary>Raised when a client service is terminated.</summary>
/// <param name="ServiceId">The terminated service ID.</param>
/// <param name="ClientId">The owning client ID.</param>
/// <param name="OccurredAt">When the change was made, UTC, as the caller of the domain method read it.</param>
public record ClientServiceTerminatedEvent(int ServiceId, int ClientId, DateTimeOffset OccurredAt) : IDomainEvent;
