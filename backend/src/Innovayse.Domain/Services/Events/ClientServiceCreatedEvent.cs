namespace Innovayse.Domain.Services.Events;

using Innovayse.Domain.Common;

/// <summary>Raised when a client orders a new service.</summary>
/// <param name="ServiceId">The service ID (0 before EF save).</param>
/// <param name="ClientId">The owning client ID.</param>
/// <param name="ProductId">The product being ordered.</param>
public record ClientServiceCreatedEvent(int ServiceId, int ClientId, int ProductId) : IDomainEvent;
