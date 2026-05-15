namespace Innovayse.Domain.Common;

/// <summary>
/// Base class for all domain entities.
/// Provides identity-based equality — two entities with the same <see cref="Id"/> are equal.
/// </summary>
public abstract class Entity
{
    /// <summary>Gets the unique identifier of this entity.</summary>
    public int Id { get; private set; }

    /// <summary>Initialises a new entity with the given identifier.</summary>
    /// <param name="id">The entity identifier. Use 0 for new (unsaved) entities.</param>
    protected Entity(int id) => Id = id;

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        if (obj is not Entity other)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (GetType() != other.GetType())
        {
            return false;
        }

        return Id == other.Id;
    }

    /// <inheritdoc/>
    public override int GetHashCode() => Id.GetHashCode();

    /// <summary>Equality operator based on entity identity.</summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns><see langword="true"/> if both entities have the same identity.</returns>
    public static bool operator ==(Entity? left, Entity? right) =>
        left?.Equals(right) ?? right is null;

    /// <summary>Inequality operator based on entity identity.</summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns><see langword="true"/> if entities have different identities.</returns>
    public static bool operator !=(Entity? left, Entity? right) => !(left == right);
}
