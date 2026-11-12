using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Domain.StatusPage;

/// <summary>
/// One component a <see cref="StatusMaintenance"/> window affects. Stored in the <c>StatusMaintenanceComponents</c> table.
/// </summary>
public sealed class StatusMaintenanceComponent : BaseEntity
{
    /// <summary>Gets the id of the window the entry belongs to.</summary>
    public int MaintenanceId { get; private set; }

    /// <summary>Gets the id of the affected component.</summary>
    public int ComponentId { get; private set; }

    /// <summary>EF Core parameterless constructor: do not call directly.</summary>
    private StatusMaintenanceComponent() { }

    /// <summary>Creates the link to an affected component.</summary>
    /// <param name="componentId">The id of the affected component.</param>
    /// <returns>A new link, not yet attached to a window.</returns>
    internal static StatusMaintenanceComponent Create(int componentId)
    {
        return new StatusMaintenanceComponent { ComponentId = componentId };
    }
}
