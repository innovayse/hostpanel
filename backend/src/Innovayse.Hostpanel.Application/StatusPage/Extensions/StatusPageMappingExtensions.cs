using Innovayse.Hostpanel.Application.StatusPage.Common;
using Innovayse.Hostpanel.Domain.StatusPage;

namespace Innovayse.Hostpanel.Application.StatusPage.Extensions;

/// <summary>Extension methods for mapping the status page aggregates to their DTOs.</summary>
public static class StatusPageMappingExtensions
{
    /// <summary>Maps a group, with its components in display order, to a <see cref="StatusComponentGroupDto"/>.</summary>
    /// <param name="group">The group to map.</param>
    /// <returns>The DTO describing the group and its components.</returns>
    public static StatusComponentGroupDto ToDto(this StatusComponentGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);

        var components = group.Components
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Id)
            .Select(c => new StatusComponentDto(c.Id, c.Name, c.Description, c.CurrentState))
            .ToList();
        return new StatusComponentGroupDto(group.Id, group.Name, components);
    }

    /// <summary>Maps an incident, with its timeline newest first, to a <see cref="StatusIncidentDto"/>.</summary>
    /// <param name="incident">The incident to map.</param>
    /// <returns>The DTO describing the incident and its timeline.</returns>
    public static StatusIncidentDto ToDto(this StatusIncident incident)
    {
        ArgumentNullException.ThrowIfNull(incident);

        var updates = incident.Updates
            .OrderByDescending(u => u.CreatedAt)
            .ThenByDescending(u => u.Id)
            .Select(u => new StatusIncidentUpdateDto(u.Id, u.Message, u.State, u.CreatedAt))
            .ToList();
        return new StatusIncidentDto(incident.Id, incident.Title, incident.State, incident.StartedAt, incident.ResolvedAt, updates);
    }
}
