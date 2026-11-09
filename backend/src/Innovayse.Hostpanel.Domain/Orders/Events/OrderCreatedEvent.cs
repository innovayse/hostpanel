using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Orders.Events;

/// <summary>Raised when a new order is placed at checkout.</summary>
/// <param name="OrderId">The order ID (0 before EF save).</param>
/// <param name="ClientId">The owning client ID.</param>
/// <param name="OccurredAt">When the change was made, UTC, as the caller of the domain method read it.</param>
public record OrderCreatedEvent(int OrderId, int ClientId, DateTimeOffset OccurredAt) : IDomainEvent;
