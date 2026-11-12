using Innovayse.Hostpanel.SharedKernel.Domain;
using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.StatusPage;

/// <summary>
/// A heading on the status page ("Hosting servers", "Control panel", "Network") with the components
/// listed under it. Stored in the <c>StatusComponentGroups</c> table.
/// </summary>
public sealed class StatusComponentGroup : BaseEntity, IAggregateRoot
{
    /// <summary>Longest Name may be: matches the <c>StatusComponentGroups.Name</c> column.</summary>
    public const int NameMaxLength = 200;

    /// <summary>Backing field for <see cref="Components"/>.</summary>
    private readonly List<StatusComponent> _components = [];

    /// <summary>Gets the group's display name.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Gets the position among the groups (ascending).</summary>
    public int DisplayOrder { get; private set; }

    /// <summary>Gets the components listed under the group.</summary>
    public IReadOnlyList<StatusComponent> Components => _components.AsReadOnly();

    /// <summary>EF Core parameterless constructor: do not call directly.</summary>
    private StatusComponentGroup() { }

    /// <summary>Creates a group with no components.</summary>
    /// <param name="name">The display name.</param>
    /// <param name="displayOrder">The position among the groups (ascending).</param>
    /// <returns>A new group.</returns>
    /// <exception cref="DomainRuleException">The name is empty or the display order is negative.</exception>
    public static StatusComponentGroup Create(string name, int displayOrder)
    {
        StatusPageRules.EnsureName(name);
        StatusPageRules.EnsureDisplayOrder(displayOrder);

        return new StatusComponentGroup { Name = name, DisplayOrder = displayOrder };
    }

    /// <summary>Renames the group.</summary>
    /// <param name="name">The new display name.</param>
    /// <exception cref="DomainRuleException">The name is empty.</exception>
    public void Rename(string name)
    {
        StatusPageRules.EnsureName(name);

        Name = name;
    }

    /// <summary>Moves the group to a position among the groups.</summary>
    /// <param name="displayOrder">The new position (ascending).</param>
    /// <exception cref="DomainRuleException">The display order is negative.</exception>
    public void Reorder(int displayOrder)
    {
        StatusPageRules.EnsureDisplayOrder(displayOrder);

        DisplayOrder = displayOrder;
    }

    /// <summary>Refuses the removal of a group that still lists components, so none is lost with it.</summary>
    /// <exception cref="DomainRuleException">The group still has components.</exception>
    public void EnsureRemovable()
    {
        if (_components.Count > 0)
        {
            throw new DomainRuleException(StatusPageErrors.GroupNotEmpty, "The group still has components.");
        }
    }

    /// <summary>Adds an operational component to the group.</summary>
    /// <param name="name">The component's display name.</param>
    /// <param name="description">A short explanation of what the component covers, or <c>null</c>.</param>
    /// <param name="displayOrder">The position within the group (ascending).</param>
    /// <returns>The component that was added.</returns>
    /// <exception cref="DomainRuleException">The name is empty or the display order is negative.</exception>
    public StatusComponent AddComponent(string name, string? description, int displayOrder)
    {
        StatusPageRules.EnsureName(name);
        StatusPageRules.EnsureDisplayOrder(displayOrder);

        var component = StatusComponent.Create(name, description, displayOrder);
        _components.Add(component);
        return component;
    }
}
