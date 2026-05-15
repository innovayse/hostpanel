namespace Innovayse.Domain.Common;

/// <summary>
/// Base class for value objects.
/// Equality is based on the values returned by <see cref="GetEqualityComponents"/>,
/// not on reference identity.
/// </summary>
public abstract class ValueObject
{
    /// <summary>
    /// Returns the components used for equality comparison.
    /// All fields / properties that define the value object's identity must be yielded here.
    /// </summary>
    /// <returns>Sequence of equality components.</returns>
    protected abstract IEnumerable<object?> GetEqualityComponents();

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        if (obj is null || obj.GetType() != GetType())
        {
            return false;
        }

        return ((ValueObject)obj).GetEqualityComponents()
            .SequenceEqual(GetEqualityComponents());
    }

    /// <inheritdoc/>
    public override int GetHashCode() =>
        GetEqualityComponents()
            .Aggregate(0, (hash, obj) => HashCode.Combine(hash, obj));

    /// <summary>Equality operator based on value components.</summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns><see langword="true"/> if both value objects have the same components.</returns>
    public static bool operator ==(ValueObject? left, ValueObject? right) =>
        left?.Equals(right) ?? right is null;

    /// <summary>Inequality operator based on value components.</summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns><see langword="true"/> if the value objects differ.</returns>
    public static bool operator !=(ValueObject? left, ValueObject? right) =>
        !(left == right);
}
