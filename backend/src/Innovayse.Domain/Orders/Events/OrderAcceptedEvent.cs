namespace Innovayse.Domain.Orders.Events;

using Innovayse.Domain.Common;

/// <summary>Raised when an admin accepts a pending order.</summary>
/// <param name="OrderId">The order ID.</param>
/// <param name="ClientId">The owning client ID.</param>
public record OrderAcceptedEvent(int OrderId, int ClientId) : IDomainEvent;
