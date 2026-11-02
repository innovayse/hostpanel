using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.SharedKernel.Domain;

/// <summary>Identity, identity-based equality and the domain-event list every entity carries.</summary>
/// <remarks>
/// The id is an <see cref="int"/> assigned by the database, not the <see cref="Guid"/> the other
/// products generate in memory: hostpanel's schema, its sixty migrations, every foreign key and
/// the numeric ids on its public API predate the standard, and changing the key type is a
/// schema migration this product deliberately does not make. An unsaved entity has id 0 and is
/// equal only to itself, so two new children of one aggregate never collapse into one.
/// </remarks>
public abstract class BaseEntity : IEquatable<BaseEntity>
{
    /// <summary>The id the database assigns on insert; 0 until then.</summary>
    private const int Unsaved = 0;

    /// <summary>
    /// Events raised since the last dispatch. Read by the persistence interceptor, never by a handler.
    /// </summary>
    /// <remarks>
    /// Stored as factories so an event can name the entity's own database-assigned id: the factory
    /// runs when the events are read, which for a creation event is after the insert has set
    /// <see cref="Id"/>, not at <c>Create</c> time when it is still 0.
    /// </remarks>
    private readonly List<Func<IDomainEvent>> _domainEvents = [];

    /// <summary>The entity's identifier; 0 until the row is saved.</summary>
    public int Id { get; protected set; }

    /// <summary>The events raised since the last <see cref="ClearDomainEvents"/>.</summary>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.Select(factory => factory()).ToList().AsReadOnly();

    /// <summary>
    /// Whether any event is waiting, answered without building one. Asked before the save, when a
    /// deferred event that names the database-assigned <see cref="Id"/> could not be built correctly yet.
    /// </summary>
    public bool HasDomainEvents => _domainEvents.Count > 0;

    /// <summary>Whether two entities are the same row; two nulls are equal.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when both are null or refer to the same saved row.</returns>
    public static bool operator ==(BaseEntity? left, BaseEntity? right)
    {
        if (left is null)
        {
            return right is null;
        }

        return left.Equals(right);
    }

    /// <summary>Whether two entities differ.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when they are not the same row.</returns>
    public static bool operator !=(BaseEntity? left, BaseEntity? right)
    {
        return !(left == right);
    }

    /// <inheritdoc />
    public bool Equals(BaseEntity? other)
    {
        if (other is null || other.GetType() != GetType())
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        // Unsaved rows have no identity yet; only a reference match counts for them.
        return Id != Unsaved && Id == other.Id;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return Equals(obj as BaseEntity);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return Id == Unsaved ? base.GetHashCode() : HashCode.Combine(GetType(), Id);
    }

    /// <summary>Forgets the raised events. Called by the dispatcher after it has handed them on.</summary>
    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }

    /// <summary>
    /// Records that something happened. Called from inside the method that changed the state, never from a handler.
    /// </summary>
    /// <param name="domainEvent">The event to raise.</param>
    protected void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(() => domainEvent);
    }

    /// <summary>
    /// Records an event that carries the entity's own <see cref="Id"/>, built when the events are read
    /// so a creation event sees the id the database assigned on insert instead of 0.
    /// </summary>
    /// <param name="eventFactory">Builds the event; called each time <see cref="DomainEvents"/> is read.</param>
    protected void AddDomainEvent(Func<IDomainEvent> eventFactory)
    {
        _domainEvents.Add(eventFactory);
    }
}
