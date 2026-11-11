using Innovayse.Hostpanel.Domain.StatusPage.Enums;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Domain.StatusPage;

/// <summary>
/// One service or piece of infrastructure the status page reports on. A component belongs to a
/// <see cref="StatusComponentGroup"/>, which creates it. Stored in the <c>StatusComponents</c> table.
/// </summary>
public sealed class StatusComponent : BaseEntity
{
    /// <summary>Longest Name may be: matches the <c>StatusComponents.Name</c> column.</summary>
    public const int NameMaxLength = 200;

    /// <summary>Longest Description may be: matches the <c>StatusComponents.Description</c> column.</summary>
    public const int DescriptionMaxLength = 1000;

    /// <summary>Gets the id of the group the component is listed under.</summary>
    public int GroupId { get; private set; }

    /// <summary>Gets the component's display name.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Gets a short explanation of what the component covers, or <c>null</c>.</summary>
    public string? Description { get; private set; }

    /// <summary>Gets the position within its group (ascending).</summary>
    public int DisplayOrder { get; private set; }

    /// <summary>Gets the state the status page shows for the component now.</summary>
    public StatusComponentState CurrentState { get; private set; }

    /// <summary>EF Core parameterless constructor: do not call directly.</summary>
    private StatusComponent() { }

    /// <summary>Creates a component that starts <see cref="StatusComponentState.Operational"/>.</summary>
    /// <param name="name">The display name.</param>
    /// <param name="description">A short explanation of what the component covers, or <c>null</c>.</param>
    /// <param name="displayOrder">The position within its group (ascending).</param>
    /// <returns>A new component, not yet attached to a group.</returns>
    internal static StatusComponent Create(string name, string? description, int displayOrder)
    {
        return new StatusComponent
        {
            Name = name,
            Description = description,
            DisplayOrder = displayOrder,
            CurrentState = StatusComponentState.Operational,
        };
    }

    /// <summary>Sets the state the status page shows for the component.</summary>
    /// <param name="state">The new state.</param>
    public void SetState(StatusComponentState state)
    {
        CurrentState = state;
    }
}
