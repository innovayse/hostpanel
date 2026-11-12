using Innovayse.Hostpanel.Domain.StatusPage.Enums;

namespace Innovayse.Hostpanel.Application.StatusPage.Common;

/// <summary>A maintenance window announced on the status page.</summary>
/// <param name="Id">The window's primary key.</param>
/// <param name="Title">The headline visitors see.</param>
/// <param name="Description">What the visitors are told about the work, or <c>null</c>.</param>
/// <param name="ScheduledStart">The UTC moment the window starts.</param>
/// <param name="ScheduledEnd">The UTC moment the window ends.</param>
/// <param name="State">Where the window stands now.</param>
/// <param name="ComponentIds">The ids of the components the window affects.</param>
public sealed record StatusMaintenanceDto(
    int Id,
    string Title,
    string? Description,
    DateTimeOffset ScheduledStart,
    DateTimeOffset ScheduledEnd,
    StatusMaintenanceState State,
    IReadOnlyList<int> ComponentIds);
