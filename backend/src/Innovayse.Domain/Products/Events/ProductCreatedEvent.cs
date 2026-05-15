namespace Innovayse.Domain.Products.Events;

using Innovayse.Domain.Common;

/// <summary>Raised when a new product is created.</summary>
/// <param name="ProductId">The newly assigned product ID (0 before EF save).</param>
/// <param name="Name">The product name.</param>
/// <param name="GroupId">The product group the product belongs to.</param>
public record ProductCreatedEvent(int ProductId, string Name, int GroupId) : IDomainEvent;
