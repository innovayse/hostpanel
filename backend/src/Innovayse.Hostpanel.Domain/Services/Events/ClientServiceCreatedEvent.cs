using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Services.Events;

/// <summary>Raised when a client orders a new service.</summary>
/// <param name="ServiceId">The service ID (0 before EF save).</param>
/// <param name="ClientId">The owning client ID.</param>
/// <param name="ProductId">The product being ordered.</param>
/// <param name="OccurredAt">When the change was made, UTC, as the caller of the domain method read it.</param>
public record ClientServiceCreatedEvent(int ServiceId, int ClientId, int ProductId, DateTimeOffset OccurredAt) : IDomainEvent;
