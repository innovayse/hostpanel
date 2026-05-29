namespace Innovayse.Domain.Orders.Events;

using Innovayse.Domain.Common;

/// <summary>Raised when a new order is placed at checkout.</summary>
/// <param name="OrderId">The order ID (0 before EF save).</param>
/// <param name="ClientId">The owning client ID.</param>
public record OrderCreatedEvent(int OrderId, int ClientId) : IDomainEvent;
