namespace Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

/// <summary>
/// An entity an operator places in a display order. One reorder use case serves every such entity
/// because each one moves through its own <see cref="SetSortOrder"/>, which keeps its invariants.
/// </summary>
public interface ISortable
{
    /// <summary>Gets the entity's identifier, the key a reorder request names it by.</summary>
    public int Id { get; }

    /// <summary>Moves the entity to a new position in the display order.</summary>
    /// <param name="sortOrder">The new position; lower values are shown first.</param>
    public void SetSortOrder(int sortOrder);
}
