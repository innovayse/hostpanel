namespace Innovayse.Domain.Common;

/// <summary>
/// Base class for aggregate roots.
/// Extends <see cref="Entity"/> with domain event collection.
/// Wolverine dispatches <see cref="DomainEvents"/> after the aggregate is saved.
/// </summary>
public abstract class AggregateRoot(int id) : Entity(id)
{
    /// <summary>Internal mutable list of domain events raised during this operation.</summary>
    private readonly List<IDomainEvent> _domainEvents = [];

    /// <summary>Gets the domain events raised since the last <see cref="ClearDomainEvents"/> call.</summary>
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>
    /// Appends a domain event to be dispatched after persistence.
    /// Call this from aggregate methods that encode business state transitions.
    /// </summary>
    /// <param name="domainEvent">The event to enqueue.</param>
    protected void AddDomainEvent(IDomainEvent domainEvent) =>
        _domainEvents.Add(domainEvent);

    /// <summary>
    /// Removes all queued domain events.
    /// Called by the unit of work after Wolverine has dispatched the events.
    /// </summary>
    public void ClearDomainEvents() => _domainEvents.Clear();
}
