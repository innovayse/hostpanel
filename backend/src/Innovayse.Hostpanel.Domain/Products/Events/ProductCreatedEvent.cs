using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Products.Events;

/// <summary>Raised when a new product is created.</summary>
/// <param name="ProductId">The newly assigned product ID (0 before EF save).</param>
/// <param name="Name">The product name.</param>
/// <param name="GroupId">The product group the product belongs to.</param>
/// <param name="OccurredAt">When the change was made, UTC, as the caller of the domain method read it.</param>
public record ProductCreatedEvent(int ProductId, string Name, int GroupId, DateTimeOffset OccurredAt) : IDomainEvent;
