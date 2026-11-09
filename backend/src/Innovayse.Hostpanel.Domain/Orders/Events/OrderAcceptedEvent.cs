using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Orders.Events;

/// <summary>Raised when an admin accepts a pending order.</summary>
/// <param name="OrderId">The order ID.</param>
/// <param name="ClientId">The owning client ID.</param>
/// <param name="OccurredAt">When the change was made, UTC, as the caller of the domain method read it.</param>
public record OrderAcceptedEvent(int OrderId, int ClientId, DateTimeOffset OccurredAt) : IDomainEvent;
