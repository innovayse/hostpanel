namespace Innovayse.Hostpanel.Application.StatusPage.Commands.UpdateStatusMaintenance;

/// <summary>Command to change a maintenance window that has not ended or been cancelled.</summary>
/// <param name="Id">
/// The window identifier; wins over any id in the body. A value type, so it is left non-nullable: a missing
/// <c>int</c> in the body binds to <c>0</c> and the route overlay (<c>command with { Id = id }</c>) replaces it.
/// </param>
/// <param name="Title">The headline visitors see.</param>
/// <param name="Description">What the visitors are told about the work, or <c>null</c>.</param>
/// <param name="ScheduledStart">The UTC moment the window starts.</param>
/// <param name="ScheduledEnd">The UTC moment the window ends.</param>
/// <param name="ComponentIds">The ids of the components the window affects.</param>
public sealed record UpdateStatusMaintenanceCommand(
    int Id,
    string Title,
    string? Description,
    DateTimeOffset ScheduledStart,
    DateTimeOffset ScheduledEnd,
    IReadOnlyList<int> ComponentIds);
