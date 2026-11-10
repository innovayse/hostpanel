using Innovayse.Hostpanel.Domain.StatusPage.Enums;

namespace Innovayse.Hostpanel.Application.StatusPage.Common;

/// <summary>Everything the top of the public status page shows: the overall state, every component and the open incidents.</summary>
/// <param name="OverallState">The most severe state among all components, or operational when there are none.</param>
/// <param name="Groups">The groups in display order, each with its components.</param>
/// <param name="ActiveIncidents">The incidents that are not resolved yet, newest first.</param>
public sealed record StatusOverviewDto(
    StatusComponentState OverallState,
    IReadOnlyList<StatusComponentGroupDto> Groups,
    IReadOnlyList<StatusIncidentDto> ActiveIncidents);
