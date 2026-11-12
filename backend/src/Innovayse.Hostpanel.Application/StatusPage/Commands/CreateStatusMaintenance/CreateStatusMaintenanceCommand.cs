namespace Innovayse.Hostpanel.Application.StatusPage.Commands.CreateStatusMaintenance;

/// <summary>Command to announce a maintenance window on the status page.</summary>
/// <param name="Title">The headline visitors see.</param>
/// <param name="Description">What the visitors are told about the work, or <c>null</c>.</param>
/// <param name="ScheduledStart">The UTC moment the window starts.</param>
/// <param name="ScheduledEnd">The UTC moment the window ends.</param>
/// <param name="ComponentIds">The ids of the components the window affects.</param>
public sealed record CreateStatusMaintenanceCommand(
    string Title,
    string? Description,
    DateTimeOffset ScheduledStart,
    DateTimeOffset ScheduledEnd,
    IReadOnlyList<int> ComponentIds);
