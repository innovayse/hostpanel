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
        EnsureValid(name, displayOrder);

        return new StatusComponentGroup { Name = name, DisplayOrder = displayOrder };
    }

    /// <summary>Adds an operational component to the group.</summary>
    /// <param name="name">The component's display name.</param>
    /// <param name="description">A short explanation of what the component covers, or <c>null</c>.</param>
    /// <param name="displayOrder">The position within the group (ascending).</param>
    /// <returns>The component that was added.</returns>
    /// <exception cref="DomainRuleException">The name is empty or the display order is negative.</exception>
    public StatusComponent AddComponent(string name, string? description, int displayOrder)
    {
        EnsureValid(name, displayOrder);

        var component = StatusComponent.Create(name, description, displayOrder);
        _components.Add(component);
        return component;
    }

    /// <summary>Refuses input that breaks an invariant shared by groups and components.</summary>
    /// <param name="name">The display name to check.</param>
    /// <param name="displayOrder">The display order to check.</param>
    /// <exception cref="DomainRuleException">The name is empty or the display order is negative.</exception>
    private static void EnsureValid(string name, int displayOrder)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainRuleException(StatusPageErrors.NameRequired, "A name is required.");
        }

        if (displayOrder < 0)
        {
            throw new DomainRuleException(StatusPageErrors.DisplayOrderNegative, "A display order cannot be negative.");
        }
    }
}
