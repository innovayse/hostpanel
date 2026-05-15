namespace Innovayse.Domain.Common;

/// <summary>
/// Marker interface for all domain events.
/// Domain events represent something that happened within the domain
/// and are dispatched by Wolverine after the aggregate is persisted.
/// </summary>
public interface IDomainEvent;
